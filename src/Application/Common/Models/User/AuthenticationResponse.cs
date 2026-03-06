namespace Application.Common.Models.User;

/// <summary>
/// Unified response for both login and registration that includes authentication token
/// </summary>
public class AuthenticationResponse : UserProfileResponse
{
    public string Token { get; set; } = string.Empty;
}
