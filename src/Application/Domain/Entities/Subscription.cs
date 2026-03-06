using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class Subscription : AuditableEntity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid PlanId { get; set; }

    public string? StripeSubscriptionId { get; set; }

    public string? StripeCustomerId { get; set; }

    public SubscriptionStatus Status { get; set; }

    public BillingInterval BillingInterval { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }

    public DateTime? CancelledAt { get; set; }

    public bool CancelAtPeriodEnd { get; set; }

    public DateTime? TrialStart { get; set; }

    public DateTime? TrialEnd { get; set; }

    public Plan Plan { get; set; } = null!;

    public Organization Organization { get; set; } = null!;
}
