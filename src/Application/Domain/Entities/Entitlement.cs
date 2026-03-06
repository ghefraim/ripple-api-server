using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class Entitlement : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public string FeatureKey { get; set; } = string.Empty;

    public string FeatureType { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public EntitlementSource Source { get; set; }

    public Guid? SourceId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? GrantedBy { get; set; }

    public string? Reason { get; set; }

    public Organization Organization { get; set; } = null!;
}
