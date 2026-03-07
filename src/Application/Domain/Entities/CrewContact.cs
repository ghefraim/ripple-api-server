using Application.Domain.Common;

namespace Application.Domain.Entities;

public class CrewContact : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid CrewId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public long? TelegramChatId { get; set; }

    public GroundCrew Crew { get; set; } = null!;
}
