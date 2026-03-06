using Application.Domain.Common;

namespace Application.Domain.Entities;

public class ActionPlan : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid DisruptionId { get; set; }

    public string? LlmRawOutput { get; set; }

    public string Actions { get; set; } = "[]";

    public Disruption Disruption { get; set; } = null!;
    public IList<Notification> Notifications { get; private set; } = new List<Notification>();
}
