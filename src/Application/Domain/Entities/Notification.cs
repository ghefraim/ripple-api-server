using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class Notification : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid DisruptionId { get; set; }

    public Guid? ActionPlanId { get; set; }

    public Guid RecipientId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Severity Severity { get; set; } = Severity.Info;

    public NotificationStatus Status { get; set; } = NotificationStatus.Sent;

    public DateTime SentAt { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public DateTime? HandledAt { get; set; }

    public Disruption Disruption { get; set; } = null!;
    public ActionPlan? ActionPlan { get; set; }
    public ApplicationUser Recipient { get; set; } = null!;
}
