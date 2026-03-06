namespace Application.Common.Models.User;

/// <summary>
/// Google-specific authentication response that includes Google profile picture
/// </summary>
public class GoogleAuthenticationResponse : AuthenticationResponse
{
    public string Name { get; set; } = string.Empty;  // Google returns 'name' not 'userName'
    public string Picture { get; set; } = string.Empty;

    // Override UserName to map from Name for consistency
    public new string UserName => Name;
}