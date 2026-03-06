using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class CascadeImpact : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid DisruptionId { get; set; }

    public Guid AffectedFlightId { get; set; }

    public CascadeImpactType ImpactType { get; set; }

    public Severity Severity { get; set; } = Severity.Info;

    public string Details { get; set; } = "{}";

    public Disruption Disruption { get; set; } = null!;
    public Flight AffectedFlight { get; set; } = null!;
}
