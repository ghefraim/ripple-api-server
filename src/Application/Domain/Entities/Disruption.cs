using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class Disruption : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid FlightId { get; set; }

    public DisruptionType Type { get; set; }

    public int? DelayMinutes { get; set; }

    public string? Reason { get; set; }

    public DisruptionStatus Status { get; set; } = DisruptionStatus.Active;

    public Guid? ReportedById { get; set; }

    public Flight Flight { get; set; } = null!;
    public ApplicationUser? ReportedBy { get; set; }
    public IList<CascadeImpact> CascadeImpacts { get; private set; } = new List<CascadeImpact>();
    public ActionPlan? ActionPlan { get; set; }
}
