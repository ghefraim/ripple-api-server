using Application.Common.Models.User;

namespace Application.Common.Interfaces;

public interface IAuthService
{
    Task<string> GetUserNameAsync(Guid userId);
    Task<AuthenticationResponse> LogInAsync(EmailLogInRequest request);
    Task<AuthenticationResponse> RegisterAsync(EmailSignUpRequest request);
    Task<UserProfileResponse> GetCurrentUserAsync();
    Task<bool> LogoutAsync();
    Task<string> RefreshTokenAsync();
    Task<GoogleAuthenticationResponse> GoogleLoginAsync(string credential);
    Task<GoogleAuthenticationResponse> GoogleCallbackAsync(string code);
    Task SendResetPasswordEmailAsync(ResetPasswordRequest request);
    Task ResetPasswordAsync(ConfirmResetPasswordRequest request);
    Task<AuthenticationResponse> ConfirmEmailAsync(ConfirmEmailRequest request);
}
