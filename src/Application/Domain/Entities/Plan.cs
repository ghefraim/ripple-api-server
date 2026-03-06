using Application.Domain.Common;
using Application.Domain.Enums;

namespace Application.Domain.Entities;

public class Plan : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? StripeProductId { get; set; }

    public string? StripeMonthlyPriceId { get; set; }

    public string? StripeAnnualPriceId { get; set; }

    public decimal MonthlyPrice { get; set; }

    public decimal AnnualPrice { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public IList<PlanFeature> Features { get; private set; } = new List<PlanFeature>();

    public IList<Subscription> Subscriptions { get; private set; } = new List<Subscription>();
}
