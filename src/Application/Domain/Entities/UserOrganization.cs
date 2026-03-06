using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class UserOrganization : ISoftDeletable
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public OrganizationRole Role { get; set; } = OrganizationRole.Member;

    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedOn { get; set; }
}
