using System.ComponentModel;

namespace Application.Common.Models.User;

/// <summary>
/// Base authentication request with common email/password fields
/// </summary>
public abstract class BaseAuthRequest
{
    /// <summary>
    /// The email of the user.
    /// </summary>
    [DefaultValue("test@example.com")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The plain-text password of the user.
    /// </summary>
    [DefaultValue("123asdsad.A")]
    public string Password { get; set; } = string.Empty;
}