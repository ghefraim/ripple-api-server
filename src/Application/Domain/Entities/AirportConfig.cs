using Application.Domain.Common;

namespace Application.Domain.Entities;

public class AirportConfig : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public string IataCode { get; set; } = string.Empty;

    public string Timezone { get; set; } = "Europe/Bucharest";

    public int MinTurnaroundMinutes { get; set; } = 35;
}
