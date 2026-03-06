using Application.Common.Configuration;
using Application.Common.Exceptions;
using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.BlobStorage;
using Application.Common.Models;
using Application.Common.Models.Organization;
using Application.Common.Models.User;
using Application.Domain.Constants;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Identity;
using Application.Infrastructure.Persistence;

using FluentValidation;

using Google.Apis.Auth;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using System.Text.Json;
using System.Text;
using System.Linq;

namespace Application.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ICookieService _cookieService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOrganizationService _organizationService;
    private readonly IMapper _mapper;
    private readonly TokenConfiguration _configuration;
    private readonly IConfiguration _config;
    private readonly IMailService _mailService;
    private readonly IConcreteStorageClient _storageClient;
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IValidator<EmailSignUpRequest> _signUpValidator;
    private readonly IValidator<EmailLogInRequest> _logInValidator;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ICookieService cookieService,
        ICurrentUserService currentUserService,
        IOrganizationService organizationService,
        IMapper mapper,
        IMailService mailService,
        IConcreteStorageClient storageClient,
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration,
        IValidator<EmailSignUpRequest> signUpValidator,
        IValidator<EmailLogInRequest> logInValidator)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _cookieService = cookieService;
        _currentUserService = currentUserService;
        _organizationService = organizationService;
        _mapper = mapper;
        _mailService = mailService;
        _storageClient = storageClient;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _configuration = TokenConfiguration.FromConfiguration(configuration);
        _config = configuration;
        _signUpValidator = signUpValidator;
        _logInValidator = logInValidator;
    }

    public async Task<string> GetUserNameAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        return user.UserName ?? string.Empty;
    }

    public async Task<AuthenticationResponse> LogInAsync(EmailLogInRequest request)
    {
        var validationResult = await _logInValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            throw new Common.Exceptions.ValidationException(validationResult.Errors);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            throw new InvalidCredentialsException("Invalid email or password");
        }

        if (!user.EmailConfirmed)
        {
            throw new EmailConfirmationException("Please confirm your email address before logging in. Check your inbox for the confirmation email.");
        }

        var userName = user.UserName ?? user.Email ?? throw new InvalidCredentialsException("User has no valid username or email");

        var result = await _signInManager.PasswordSignInAsync(
            userName,
            request.Password,
            isPersistent: true,
            lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            throw new InvalidCredentialsException("Invalid email or password");
        }

        user = await _organizationService.GetUserWithOrganizationsAsync(user.Id);
        await _organizationService.EnsureUserHasDefaultOrganizationAsync(user);
        await PersistChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);
        var highestRole = await _userManager.GetHighestRole(user);
        var selectedUserOrg = user.UserOrganizations.FirstOrDefault(uo => !uo.IsDeleted);
        var selectedOrganizationId = selectedUserOrg?.OrganizationId;
        var selectedOrganizationRole = selectedUserOrg?.Role;

        var accessToken = _tokenService.GenerateAccessToken(user, roles, selectedOrganizationId, selectedOrganizationRole);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await CreateUserSessionAsync(user, refreshToken);
        _cookieService.SetRefreshToken(refreshToken);

        return await BuildAuthenticationResponseAsync(user, highestRole, accessToken);
    }

    public async Task<AuthenticationResponse> RegisterAsync(EmailSignUpRequest request)
    {
        var validationResult = await _signUpValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            throw new Common.Exceptions.ValidationException(validationResult.Errors);
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);

        if (existingUser != null)
        {
            throw new ConflictException($"User with email {request.Email} already exists");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = false, // Explicitly set to false for security
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BadHttpRequestException($"Failed to create user: {errors}");
        }

        // Assign default role
        await IdentityInitializer.AssignDefaultRoleAsync(_userManager, user);

        // Generate email confirmation token
        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        // Get the frontend URL from configuration
        var frontendUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var confirmationLink = $"{frontendUrl}/confirm-email?token={Uri.EscapeDataString(confirmationToken)}&email={Uri.EscapeDataString(request.Email)}";

        var emailBody = $@"
            <h2>Welcome to Our Platform!</h2>
            <p>Hello {user.UserName},</p>
            <p>Thank you for registering. Please confirm your email address by clicking the link below:</p>
            <p><a href='{confirmationLink}'>Confirm Email Address</a></p>
            <p>If you didn't create an account, please ignore this email.</p>
            <p>This link will expire in 24 hours.</p>
            <br/>
            <p>Best regards,<br/>The Team</p>
        ";

        try
        {
            await _mailService.SendEmailAsync(request.Email, "Confirm Your Email", emailBody);
        }
        catch (Exception ex)
        {
            // Log the error but don't fail registration
            // User can request a new confirmation email later
            throw new Exception("Failed to send confirmation email", ex);
        }

        var trackedUser = await _organizationService.GetUserWithOrganizationsAsync(user.Id);
        await _organizationService.EnsureUserHasDefaultOrganizationAsync(trackedUser);
        await PersistChangesAsync();

        var highestRole = await _userManager.GetHighestRole(trackedUser);
        return await BuildAuthenticationResponseAsync(trackedUser, highestRole, string.Empty);
    }

    public async Task<bool> LogoutAsync()
    {
        var refreshToken = _cookieService.GetRefreshToken();

        if (!string.IsNullOrEmpty(refreshToken))
        {
            // Find and deactivate the current session
            var userSession = await _context.UserSessions
                .FirstOrDefaultAsync(us => us.RefreshToken == refreshToken && us.IsActive);

            if (userSession != null)
            {
                userSession.IsActive = false;
                await PersistChangesAsync();
            }
        }

        // Clear identity and cookies
        await _signInManager.SignOutAsync();
        _cookieService.DeleteRefreshToken();
        return true;
    }

    public async Task<string> RefreshTokenAsync()
    {
        var refreshToken = _cookieService.GetRefreshToken();

        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token not found");
        }

        // Look up the user session by refresh token (no need for JWT context)
        var userSession = await _context.UserSessions
            .Include(us => us.User)
                .ThenInclude(u => u.UserOrganizations)
                    .ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(us => us.RefreshToken == refreshToken && us.IsActive);

        if (userSession == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        // Check if refresh token is expired
        if (userSession.RefreshTokenExpiryDate <= DateTime.UtcNow)
        {
            // Mark session as inactive
            userSession.IsActive = false;
            await PersistChangesAsync();
            throw new UnauthorizedAccessException("Refresh token expired");
        }

        var user = userSession.User;
        await _organizationService.EnsureUserHasDefaultOrganizationAsync(user);
        await PersistChangesAsync();

        // Try to extract organization ID from the old/expired access token
        Guid? selectedOrganizationId = null;
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var oldToken = authHeader.Substring("Bearer ".Length).Trim();
            selectedOrganizationId = _tokenService.ExtractOrganizationIdFromToken(oldToken);
        }

        // If no organization ID found in token, default to first organization
        if (selectedOrganizationId == null || selectedOrganizationId == Guid.Empty)
        {
            var firstOrg = user.UserOrganizations.FirstOrDefault(uo => !uo.IsDeleted);
            selectedOrganizationId = firstOrg?.OrganizationId;
        }

        // Verify user still has access to this organization
        if (selectedOrganizationId != null && selectedOrganizationId != Guid.Empty)
        {
            var hasAccess = user.UserOrganizations.Any(uo => uo.OrganizationId == selectedOrganizationId && !uo.IsDeleted);
            if (!hasAccess)
            {
                // User lost access, default to first organization
                var firstOrg = user.UserOrganizations.FirstOrDefault(uo => !uo.IsDeleted);
                selectedOrganizationId = firstOrg?.OrganizationId;
            }
        }

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);

        // Get organization role for the selected organization
        var selectedUserOrg = user.UserOrganizations.FirstOrDefault(uo => uo.OrganizationId == selectedOrganizationId && !uo.IsDeleted);
        var selectedOrganizationRole = selectedUserOrg?.Role;

        // Generate new tokens
        var newAccessToken = _tokenService.GenerateAccessToken(user, roles, selectedOrganizationId, selectedOrganizationRole);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        // Update the user session with new refresh token (token rotation)
        userSession.RefreshToken = newRefreshToken;
        userSession.RefreshTokenExpiryDate = _tokenService.GetRefreshTokenExpiry();
        userSession.LastUsedDate = DateTime.UtcNow;

        await PersistChangesAsync();

        // Update cookies
        _cookieService.SetRefreshToken(newRefreshToken);

        return newAccessToken;
    }

    public async Task<UserProfileResponse> GetCurrentUserAsync()
    {
        var userId = _currentUserService.UserId;

        if (userId == null)
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            throw new UnauthorizedAccessException("User not found");
        }

        var user = await _organizationService.GetUserWithOrganizationsAsync(userGuid);

        await _organizationService.EnsureUserHasDefaultOrganizationAsync(user);
        await PersistChangesAsync();

        var highestRole = await _userManager.GetHighestRole(user);
        return await BuildUserProfileResponseAsync(user, highestRole);
    }

    public async Task<GoogleAuthenticationResponse> GoogleLoginAsync(string credential)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(credential);

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(payload.Email);

        if (existingUser == null)
        {
            // Create a new user
            var newUser = new ApplicationUser
            {
                UserName = payload.Email,
                Email = payload.Email,
                EmailConfirmed = true,
                AvatarUrl = payload.Picture,
            };

            var result = await _userManager.CreateAsync(newUser);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new UnauthorizedAccessException($"Failed to create user: {errors}");
            }

            // Assign default role
            await IdentityInitializer.AssignDefaultRoleAsync(_userManager, newUser);

            existingUser = newUser;
        }

        // Sign in the user
        await _signInManager.SignInAsync(existingUser, isPersistent: true);

        var user = await _organizationService.GetUserWithOrganizationsAsync(existingUser.Id);
        await _organizationService.EnsureUserHasDefaultOrganizationAsync(user);
        await PersistChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);
        var highestRole = await _userManager.GetHighestRole(user);
        var selectedUserOrg = user.UserOrganizations.FirstOrDefault(uo => !uo.IsDeleted);
        var selectedOrganizationId = selectedUserOrg?.OrganizationId;
        var selectedOrganizationRole = selectedUserOrg?.Role;

        var accessToken = _tokenService.GenerateAccessToken(user, roles, selectedOrganizationId, selectedOrganizationRole);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await CreateUserSessionAsync(user, refreshToken);
        _cookieService.SetRefreshToken(refreshToken);

        var authResponse = await BuildAuthenticationResponseAsync(user, highestRole, accessToken);
        var response = new GoogleAuthenticationResponse
        {
            Token = authResponse.Token,
            Id = authResponse.Id,
            Email = authResponse.Email,
            AvatarUrl = authResponse.AvatarUrl,
            Organizations = authResponse.Organizations,
            SelectedOrganization = authResponse.SelectedOrganization,
            SelectedOrganizationId = authResponse.SelectedOrganizationId,
            Role = authResponse.Role,
            Name = payload.Name,
            Picture = payload.Picture ?? authResponse.AvatarUrl ?? string.Empty,
        };

        ((AuthenticationResponse)response).UserName = authResponse.UserName;

        return response;
    }

    public async Task<GoogleAuthenticationResponse> GoogleCallbackAsync(string code)
    {
        // Exchange authorization code for tokens
        var tokenResponse = await ExchangeCodeForTokensAsync(code);

        // Get user info from Google
        var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken);

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(userInfo.Email);

        if (existingUser == null)
        {
            // Create a new user
            var newUser = new ApplicationUser
            {
                UserName = userInfo.Email,
                Email = userInfo.Email,
                EmailConfirmed = true,
                AvatarUrl = userInfo.Picture,
            };

            var result = await _userManager.CreateAsync(newUser);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new UnauthorizedAccessException($"Failed to create user: {errors}");
            }

            // Assign default role
            await IdentityInitializer.AssignDefaultRoleAsync(_userManager, newUser);

            existingUser = newUser;
        }

        // Sign in the user
        await _signInManager.SignInAsync(existingUser, isPersistent: true);

        var user = await _organizationService.GetUserWithOrganizationsAsync(existingUser.Id);
        await _organizationService.EnsureUserHasDefaultOrganizationAsync(user);
        await PersistChangesAsync();

        var roles = await _userManager.GetRolesAsync(user);
        var highestRole = await _userManager.GetHighestRole(user);
        var selectedUserOrg = user.UserOrganizations.FirstOrDefault(uo => !uo.IsDeleted);
        var selectedOrganizationId = selectedUserOrg?.OrganizationId;
        var selectedOrganizationRole = selectedUserOrg?.Role;

        var accessToken = _tokenService.GenerateAccessToken(user, roles, selectedOrganizationId, selectedOrganizationRole);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await CreateUserSessionAsync(user, refreshToken);
        _cookieService.SetRefreshToken(refreshToken);

        var authResponse = await BuildAuthenticationResponseAsync(user, highestRole, accessToken);
        var response = new GoogleAuthenticationResponse
        {
            Token = authResponse.Token,
            Id = authResponse.Id,
            Email = authResponse.Email,
            AvatarUrl = authResponse.AvatarUrl,
            Organizations = authResponse.Organizations,
            SelectedOrganization = authResponse.SelectedOrganization,
            SelectedOrganizationId = authResponse.SelectedOrganizationId,
            Role = authResponse.Role,
            Name = userInfo.Name,
            Picture = userInfo.Picture ?? authResponse.AvatarUrl ?? string.Empty,
        };

        ((AuthenticationResponse)response).UserName = authResponse.UserName;

        return response;
    }

    private async Task<AuthenticationResponse> BuildAuthenticationResponseAsync(ApplicationUser user, Role highestRole, string token)
    {
        var profile = await BuildUserProfileResponseAsync(user, highestRole);

        return new AuthenticationResponse
        {
            Id = profile.Id,
            Email = profile.Email,
            UserName = profile.UserName,
            Role = profile.Role,
            AvatarUrl = profile.AvatarUrl,
            Organizations = profile.Organizations,
            SelectedOrganization = profile.SelectedOrganization,
            SelectedOrganizationId = profile.SelectedOrganizationId,
            Token = token,
        };
    }

    private async Task<UserProfileResponse> BuildUserProfileResponseAsync(ApplicationUser user, Role highestRole)
    {
        var response = _mapper.Map<UserProfileResponse>(user);
        response.Role = highestRole;
        if (string.IsNullOrWhiteSpace(response.UserName))
        {
            response.UserName = user.UserName ?? user.Email ?? string.Empty;
        }

        var memberships = user.UserOrganizations
            .Where(uo => !uo.IsDeleted)
            .Select(uo => new UserOrganizationResponse
            {
                OrganizationId = uo.OrganizationId,
                OrganizationName = uo.Organization.Name,
                Role = uo.Role,
            })
            .OrderBy(m => m.OrganizationName)
            .ToList();

        response.Organizations = memberships;

        // Determine selected organization from JWT token claim
        var selectedOrganizationId = _currentUserService.OrganizationId;
        if (selectedOrganizationId == null || selectedOrganizationId == Guid.Empty)
        {
            // If no org in token, default to first organization
            var firstOrg = memberships.FirstOrDefault();
            response.SelectedOrganization = firstOrg;
            response.SelectedOrganizationId = firstOrg?.OrganizationId;
        }
        else
        {
            // Find the organization matching the token claim
            response.SelectedOrganization = memberships.FirstOrDefault(m => m.OrganizationId == selectedOrganizationId);
            response.SelectedOrganizationId = selectedOrganizationId;
        }
        response.AvatarUrl = await ResolveAvatarUrlAsync(user, response.AvatarUrl);

        return response;
    }

    private Task<string?> ResolveAvatarUrlAsync(ApplicationUser user, string? fallbackUrl)
    {
        if (!string.IsNullOrEmpty(user.AvatarBlobName))
        {
            try
            {
                var sasUri = _storageClient.GetFileSasUri(user.AvatarBlobName, TimeSpan.FromHours(24));
                return Task.FromResult<string?>(sasUri.AbsoluteUri);
            }
            catch
            {
                return Task.FromResult(fallbackUrl ?? user.AvatarUrl);
            }
        }

        return Task.FromResult(fallbackUrl ?? user.AvatarUrl);
    }

    private async Task PersistChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (DbUpdateException ex)
        {
            // Log the exception details here as needed
            throw new Exception("An error occurred while saving changes to the database.", ex);
        }
    }

    private async Task<UserSession> CreateUserSessionAsync(ApplicationUser user, string refreshToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        var userSession = new UserSession
        {
            UserId = user.Id,
            RefreshToken = refreshToken,
            RefreshTokenExpiryDate = _tokenService.GetRefreshTokenExpiry(),
            DeviceInfo = ExtractDeviceInfo(httpContext),
            IpAddress = ExtractIpAddress(httpContext),
            UserAgent = ExtractUserAgent(httpContext),
            LastUsedDate = DateTime.UtcNow,
            IsActive = true,
        };

        _context.UserSessions.Add(userSession);
        await PersistChangesAsync();

        return userSession;
    }

    private static string ExtractDeviceInfo(HttpContext? httpContext)
    {
        if (httpContext?.Request.Headers.TryGetValue("User-Agent", out var userAgentValues) == true)
        {
            var userAgent = userAgentValues.ToString();

            // Simple device detection based on user agent
            if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
            {
                return "Mobile Device";
            }

            if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            {
                return "Chrome Browser";
            }

            if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            {
                return "Firefox Browser";
            }

            if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase))
            {
                return "Safari Browser";
            }

            if (userAgent.Contains("Edge", StringComparison.OrdinalIgnoreCase))
            {
                return "Edge Browser";
            }
        }

        return "Unknown Device";
    }

    private static string? ExtractIpAddress(HttpContext? httpContext)
    {
        if (httpContext == null)
        {
            return null;
        }

        // Check for forwarded IP first (in case of proxy/load balancer)
        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ip = forwardedFor.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? ExtractUserAgent(HttpContext? httpContext)
    {
        if (httpContext?.Request.Headers.TryGetValue("User-Agent", out var userAgentValues) == true)
        {
            return userAgentValues.ToString();
        }

        return null;
    }

    private async Task<GoogleTokenResponse> ExchangeCodeForTokensAsync(string code)
    {
        var clientId = _configuration.GoogleClientId;
        var clientSecret = _config["Authentication:Google:ClientSecret"] ?? throw new ArgumentNullException("Authentication:Google:ClientSecret");
        var redirectUri = _config["Authentication:Google:RedirectUri"] ?? throw new ArgumentNullException("Authentication:Google:RedirectUri");

        var tokenRequest = new
        {
            code = code,
            client_id = clientId,
            client_secret = clientSecret,
            redirect_uri = redirectUri,
            grant_type = "authorization_code",
        };

        var json = JsonSerializer.Serialize(tokenRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new UnauthorizedAccessException($"Failed to exchange code for tokens: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return tokenResponse ?? throw new UnauthorizedAccessException("Invalid token response from Google");
    }

    private async Task<GoogleUserInfo> GetGoogleUserInfoAsync(string accessToken)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new UnauthorizedAccessException($"Failed to get user info from Google: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(responseContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return userInfo ?? throw new UnauthorizedAccessException("Invalid user info response from Google");
    }

    public async Task SendResetPasswordEmailAsync(ResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Generate password reset token
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Get the frontend URL from configuration
        var frontendUrl = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var resetLink = $"{frontendUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(request.Email)}";

        var emailBody = $@"
            <h2>Password Reset Request</h2>
            <p>Hello {user.UserName},</p>
            <p>We received a request to reset your password. Click the link below to reset your password:</p>
            <p><a href='{resetLink}'>Reset Password</a></p>
            <p>If you didn't request this, please ignore this email.</p>
            <p>This link will expire in 24 hours.</p>
            <br/>
            <p>Best regards,<br/>The Team</p>
        ";

        try
        {
            await _mailService.SendEmailAsync(request.Email, "Reset Password", emailBody);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to send reset password email", ex);
        }
    }

    public async Task ResetPasswordAsync(ConfirmResetPasswordRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.EmailConfirmed)
        {
            throw new UnauthorizedAccessException("User not found or email not confirmed");
        }

        // Reset the password using the token
        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BadHttpRequestException($"Failed to reset password: {errors}");
        }
    }

    public async Task<AuthenticationResponse> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Confirm the email using the token
        var result = await _userManager.ConfirmEmailAsync(user, request.Token);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new EmailConfirmationException($"Failed to confirm email: {errors}");
        }

        // Email confirmed successfully - now sign in the user
        await _signInManager.SignInAsync(user, isPersistent: true);

        var trackedUser = await _organizationService.GetUserWithOrganizationsAsync(user.Id);
        await _organizationService.EnsureUserHasDefaultOrganizationAsync(trackedUser);
        await PersistChangesAsync();

        var roles = await _userManager.GetRolesAsync(trackedUser);
        var highestRole = await _userManager.GetHighestRole(trackedUser);
        var selectedUserOrg = trackedUser.UserOrganizations.FirstOrDefault(uo => !uo.IsDeleted);
        var selectedOrganizationId = selectedUserOrg?.OrganizationId;
        var selectedOrganizationRole = selectedUserOrg?.Role;

        var accessToken = _tokenService.GenerateAccessToken(trackedUser, roles, selectedOrganizationId, selectedOrganizationRole);
        var refreshToken = _tokenService.GenerateRefreshToken();

        await CreateUserSessionAsync(trackedUser, refreshToken);
        _cookieService.SetRefreshToken(refreshToken);

        return await BuildAuthenticationResponseAsync(trackedUser, highestRole, accessToken);
    }
}
