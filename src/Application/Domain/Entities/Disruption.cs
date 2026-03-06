using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class Disruption : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid AirportId { get; set; }

    public Guid FlightId { get; set; }

    public DisruptionType Type { get; set; }

    public string DetailsJson { get; set; } = "{}";

    public string? ReportedBy { get; set; }

    public DateTime ReportedAt { get; set; }

    public DisruptionStatus Status { get; set; } = DisruptionStatus.Active;

    public Guid? ReportedById { get; set; }

    public AirportConfig? Airport { get; set; }
    public Flight Flight { get; set; } = null!;
    public ApplicationUser? ReportedByUser { get; set; }
    public IList<CascadeImpact> CascadeImpacts { get; private set; } = new List<CascadeImpact>();
    public ActionPlan? ActionPlan { get; set; }
}
