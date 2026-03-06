using Application.Common.Attributes;
using Application.Domain.Common;

using Microsoft.AspNetCore.Identity;

namespace Application.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>, ISoftDeletable
{
    public ICollection<TodoList> TodoLists { get; set; } = new List<TodoList>();
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    public ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
    public ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();

    // Avatar properties
    public string? AvatarBlobName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime? AvatarUploadedAt { get; set; }


    [ExcludeFromAudit]
    public DateTime CreatedOn { get; set; }
    [ExcludeFromAudit]
    public string? CreatedBy { get; set; }
    [ExcludeFromAudit]
    public DateTime? UpdatedOn { get; set; }
    [ExcludeFromAudit]
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedOn { get; set; }
}
