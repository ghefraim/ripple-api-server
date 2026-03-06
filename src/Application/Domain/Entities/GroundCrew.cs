using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class GroundCrew : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public TimeOnly ShiftStart { get; set; }

    public TimeOnly ShiftEnd { get; set; }

    public CrewStatus Status { get; set; } = CrewStatus.Available;

    public IList<Flight> AssignedFlights { get; private set; } = new List<Flight>();
}
