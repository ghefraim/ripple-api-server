using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class Flight : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid AirportId { get; set; }

    public string FlightNumber { get; set; } = string.Empty;

    public string? Airline { get; set; }

    public string? Origin { get; set; }

    public string? Destination { get; set; }

    public FlightDirection Direction { get; set; } = FlightDirection.Arrival;

    public FlightType FlightType { get; set; } = FlightType.Domestic;

    public FlightStatus Status { get; set; } = FlightStatus.OnTime;

    public DateTime ScheduledTime { get; set; }

    public DateTime? EstimatedTime { get; set; }

    public DateTime? ActualTime { get; set; }

    public Guid? GateId { get; set; }
    public Guid? CrewId { get; set; }
    public Guid? TurnaroundPairId { get; set; }

    public AirportConfig? Airport { get; set; }
    public Gate? Gate { get; set; }
    public GroundCrew? Crew { get; set; }
    public Flight? TurnaroundPair { get; set; }
    public IList<Disruption> Disruptions { get; private set; } = new List<Disruption>();
    public IList<CascadeImpact> CascadeImpacts { get; private set; } = new List<CascadeImpact>();
}
