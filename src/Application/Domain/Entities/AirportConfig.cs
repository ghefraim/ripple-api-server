using Application.Domain.Common;

namespace Application.Domain.Entities;

public class AirportConfig : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public string IataCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Timezone { get; set; } = "Europe/Bucharest";

    public string? ConfigJson { get; set; }

    public int MinTurnaroundMinutes { get; set; } = 35;

    public IList<Gate> Gates { get; private set; } = new List<Gate>();
    public IList<Flight> Flights { get; private set; } = new List<Flight>();
    public IList<GroundCrew> GroundCrews { get; private set; } = new List<GroundCrew>();
}
