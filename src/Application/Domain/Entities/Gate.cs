using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class Gate : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid AirportId { get; set; }

    public string Code { get; set; } = string.Empty;

    public GateType GateType { get; set; } = GateType.Both;

    public GateSizeCategory SizeCategory { get; set; } = GateSizeCategory.Narrow;

    public bool IsActive { get; set; } = true;

    public AirportConfig? Airport { get; set; }
    public IList<Flight> Flights { get; private set; } = new List<Flight>();
}
