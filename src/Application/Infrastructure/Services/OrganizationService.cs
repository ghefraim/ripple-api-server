using Application.Common.Exceptions;
using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.BlobStorage;
using Application.Common.Models.Organization;
using Application.Common.Models.User;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;

using FluentValidation.Results;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Infrastructure.Services;

public class OrganizationService : IOrganizationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IConcreteStorageClient _storageClient;
    private readonly ApplicationDbContext _context;

    public OrganizationService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IConcreteStorageClient storageClient,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _currentUserService = currentUserService;
        _mapper = mapper;
        _storageClient = storageClient;
        _context = context;
    }

    public async Task<AuthenticationResponse> SelectOrganizationAsync(SelectOrganizationRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            var failures = new List<ValidationFailure>
            {
                new("OrganizationId", "Organization ID is required.")
            };
            throw new ValidationException(failures);
        }

        var userId = _currentUserService.UserId;
        if (userId == null || !Guid.TryParse(userId, out var userGuid))
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        var user = await GetUserWithOrganizationsAsync(userGuid);

        var membership = user.UserOrganizations.FirstOrDefault(uo => uo.OrganizationId == request.OrganizationId && !uo.IsDeleted);
        if (membership == null)
        {
            throw new NotFoundException("Organization", request.OrganizationId);
        }

        var roles = await _userManager.GetRolesAsync(user);
        var highestRole = await _userManager.GetHighestRole(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles, membership.OrganizationId, membership.Role);

        return await BuildAuthenticationResponseAsync(user, highestRole, accessToken);
    }

    public async Task<AuthenticationResponse> CreateOrganizationAsync(CreateOrganizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            var failures = new List<ValidationFailure>
            {
                new("Name", "Organization name is required."),
            };
            throw new ValidationException(failures);
        }

        var userId = _currentUserService.UserId;
        if (userId == null || !Guid.TryParse(userId, out var userGuid))
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        var user = await GetUserWithOrganizationsAsync(userGuid);

        var now = DateTime.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedOn = now,
            CreatedBy = userId,
        };

        var membership = new UserOrganization
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Organization = organization,
            Role = OrganizationRole.Owner,
            CreatedOn = now,
            CreatedBy = userId,
        };

        await _context.Organizations.AddAsync(organization);
        await _context.UserOrganizations.AddAsync(membership);
        organization.UserOrganizations.Add(membership);

        await PersistChangesAsync();

        user = await GetUserWithOrganizationsAsync(userGuid);

        var roles = await _userManager.GetRolesAsync(user);
        var highestRole = await _userManager.GetHighestRole(user);

        // Use new organization if MakeSelected is true, otherwise use existing org from token
        Guid? selectedOrganizationId;
        OrganizationRole? selectedOrganizationRole;
        if (request.MakeSelected)
        {
            selectedOrganizationId = organization.Id;
            selectedOrganizationRole = OrganizationRole.Owner; // User is always Owner of newly created org
        }
        else
        {
            // Keep current organization from token
            selectedOrganizationId = _currentUserService.OrganizationId;
            // If no org in token, default to first organization
            if (selectedOrganizationId == null || selectedOrganizationId == Guid.Empty)
            {
                var firstOrg = user.UserOrganizations.FirstOrDefault(uo => !uo.IsDeleted);
                selectedOrganizationId = firstOrg?.OrganizationId;
                selectedOrganizationRole = firstOrg?.Role;
            }
            else
            {
                var selectedOrg = user.UserOrganizations.FirstOrDefault(uo => uo.OrganizationId == selectedOrganizationId && !uo.IsDeleted);
                selectedOrganizationRole = selectedOrg?.Role;
            }
        }

        var accessToken = _tokenService.GenerateAccessToken(user, roles, selectedOrganizationId, selectedOrganizationRole);

        return await BuildAuthenticationResponseAsync(user, highestRole, accessToken, selectedOrganizationId);
    }

    public async Task EnsureUserHasDefaultOrganizationAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (user.UserOrganizations.Any(uo => !uo.IsDeleted))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = GenerateDefaultOrganizationName(user),
            CreatedOn = now,
            CreatedBy = user.Id.ToString(),
        };

        var membership = new UserOrganization
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            Organization = organization,
            Role = OrganizationRole.Owner,
            CreatedOn = now,
            CreatedBy = user.Id.ToString(),
        };

        await _context.Organizations.AddAsync(organization, cancellationToken);
        await _context.UserOrganizations.AddAsync(membership, cancellationToken);
        organization.UserOrganizations.Add(membership);
        user.UserOrganizations.Add(membership);
    }

    public async Task<ApplicationUser> GetUserWithOrganizationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .Include(u => u.UserOrganizations)
                .ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        return user;
    }

    private static string GenerateDefaultOrganizationName(ApplicationUser user)
    {
        var baseName = user.UserName ?? user.Email ?? "Workspace";

        if (baseName.Contains('@'))
        {
            baseName = baseName.Split('@')[0];
        }

        baseName = baseName.Trim();
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "Workspace";
        }

        return $"{baseName}'s Workspace";
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
            throw new Exception("An error occurred while saving changes to the database.", ex);
        }
    }

    private async Task<AuthenticationResponse> BuildAuthenticationResponseAsync(ApplicationUser user, Role highestRole, string token, Guid? selectedOrgIdOverride = null)
    {
        var profile = await BuildUserProfileResponseAsync(user, highestRole, selectedOrgIdOverride);

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

    private async Task<UserProfileResponse> BuildUserProfileResponseAsync(ApplicationUser user, Role highestRole, Guid? selectedOrgIdOverride = null)
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

        // Use override if provided, otherwise fall back to JWT token claim
        var selectedOrganizationId = selectedOrgIdOverride ?? _currentUserService.OrganizationId;
        if (selectedOrganizationId == null || selectedOrganizationId == Guid.Empty)
        {
            // If no org selected, default to first organization
            var firstOrg = memberships.FirstOrDefault();
            response.SelectedOrganization = firstOrg;
            response.SelectedOrganizationId = firstOrg?.OrganizationId;
        }
        else
        {
            // Find the organization matching the selected ID
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
}
