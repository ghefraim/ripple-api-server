using Application.Common.Attributes;

namespace Application.Domain.Common;

public abstract class AuditableEntity : BaseEntity, ISoftDeletable
{
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