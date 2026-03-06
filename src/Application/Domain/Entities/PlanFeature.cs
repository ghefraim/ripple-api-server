using Application.Domain.Common;

namespace Application.Domain.Entities;

public class PlanFeature : AuditableEntity
{
    public Guid PlanId { get; set; }

    public string FeatureKey { get; set; } = string.Empty;

    public string FeatureType { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public Plan Plan { get; set; } = null!;
}
