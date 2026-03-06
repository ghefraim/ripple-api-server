using Application.Domain.Common;

namespace Application.Domain.Entities;

public class OperationalRule : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string RuleDefinition { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    public Guid? CreatedById { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
