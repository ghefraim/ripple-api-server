using Application.Domain.Enums;

namespace Application.Common.Models.User;

/// <summary>
/// Base user information shared across all user-related responses
/// </summary>
public abstract class BaseUserInfo
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.User;
}