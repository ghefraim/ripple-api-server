using Application.Common.Interfaces;
using Application.Domain.Enums;
using Application.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using LocalPlan = Application.Domain.Entities.Plan;
using LocalSubscription = Application.Domain.Entities.Subscription;

namespace Application.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        ApplicationDbContext context,
        IStripeService stripeService,
        ILogger<SubscriptionService> logger)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
    }

    public async Task<LocalSubscription?> GetCurrentSubscriptionAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _context.Subscriptions
            .IgnoreQueryFilters()
            .Include(s => s.Plan)
            .ThenInclude(p => p.Features)
            .Where(s => s.OrganizationId == organizationId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedOn)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LocalSubscription> SyncFromStripeAsync(global::Stripe.Subscription stripeSub, CancellationToken cancellationToken = default)
    {
        var subscription = await _context.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id && !s.IsDeleted, cancellationToken);

        var organizationId = GetOrganizationIdFromMetadata(stripeSub);
        var plan = await GetPlanFromStripePriceAsync(stripeSub, cancellationToken);

        if (subscription == null)
        {
            subscription = new LocalSubscription
            {
                OrganizationId = organizationId,
                StripeSubscriptionId = stripeSub.Id,
                StripeCustomerId = stripeSub.CustomerId,
            };
            _context.Subscriptions.Add(subscription);
        }

        subscription.PlanId = plan.Id;
        subscription.Status = MapStripeStatus(stripeSub.Status);
        subscription.BillingInterval = GetBillingInterval(stripeSub);

        // In Stripe.net v48+, CurrentPeriodStart/End moved from Subscription to SubscriptionItem
        var firstItem = stripeSub.Items.Data.FirstOrDefault();
        subscription.CurrentPeriodStart = firstItem?.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = firstItem?.CurrentPeriodEnd;

        subscription.CancelAtPeriodEnd = stripeSub.CancelAtPeriodEnd;
        subscription.CancelledAt = stripeSub.CanceledAt;
        subscription.TrialStart = stripeSub.TrialStart;
        subscription.TrialEnd = stripeSub.TrialEnd;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Synced subscription {SubscriptionId} for organization {OrganizationId} with status {Status}",
            stripeSub.Id, organizationId, subscription.Status);

        return subscription;
    }

    public async Task<Application.Domain.Entities.BillingCustomer> GetOrCreateBillingCustomerAsync(
        Guid organizationId,
        string email,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var billingCustomer = await _context.BillingCustomers
            .FirstOrDefaultAsync(
                bc => bc.EntityType == BillableEntityType.Organization &&
                      bc.EntityId == organizationId &&
                      !bc.IsDeleted,
                cancellationToken);

        if (billingCustomer != null)
        {
            return billingCustomer;
        }

        var idempotencyKey = $"create_customer_{organizationId}";
        var stripeCustomer = await _stripeService.CreateCustomerAsync(
            email,
            name,
            new Dictionary<string, string>
            {
                ["organizationId"] = organizationId.ToString(),
            },
            idempotencyKey,
            cancellationToken);

        billingCustomer = new Application.Domain.Entities.BillingCustomer
        {
            EntityType = BillableEntityType.Organization,
            EntityId = organizationId,
            StripeCustomerId = stripeCustomer.Id,
            Email = email,
            Name = name,
        };

        _context.BillingCustomers.Add(billingCustomer);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created billing customer {CustomerId} for organization {OrganizationId}",
            stripeCustomer.Id, organizationId);

        return billingCustomer;
    }

    public async Task<LocalSubscription?> SyncFromStripeByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var billingCustomer = await _context.BillingCustomers
            .FirstOrDefaultAsync(
                bc => bc.EntityType == BillableEntityType.Organization &&
                      bc.EntityId == organizationId &&
                      !bc.IsDeleted,
                cancellationToken);

        if (billingCustomer == null)
        {
            _logger.LogInformation("No billing customer found for organization {OrganizationId}, nothing to sync", organizationId);
            return null;
        }

        var stripeSubscriptions = await _stripeService.ListSubscriptionsAsync(billingCustomer.StripeCustomerId, cancellationToken);

        // Find the most recent active subscription
        var activeSubscription = stripeSubscriptions
            .Where(s => s.Status is "active" or "trialing")
            .OrderByDescending(s => s.Created)
            .FirstOrDefault();

        if (activeSubscription == null)
        {
            _logger.LogInformation("No active Stripe subscription found for customer {CustomerId}", billingCustomer.StripeCustomerId);
            return null;
        }

        // Ensure metadata has organizationId (may be missing if subscription was created outside our flow)
        if (!activeSubscription.Metadata.ContainsKey("organizationId"))
        {
            activeSubscription.Metadata["organizationId"] = organizationId.ToString();
        }

        return await SyncFromStripeAsync(activeSubscription, cancellationToken);
    }

    private static Guid GetOrganizationIdFromMetadata(global::Stripe.Subscription stripeSub)
    {
        if (stripeSub.Metadata.TryGetValue("organizationId", out var orgIdString) &&
            Guid.TryParse(orgIdString, out var orgId))
        {
            return orgId;
        }

        throw new InvalidOperationException($"Organization ID not found in subscription metadata for {stripeSub.Id}");
    }

    private async Task<LocalPlan> GetPlanFromStripePriceAsync(global::Stripe.Subscription stripeSub, CancellationToken cancellationToken)
    {
        var priceId = stripeSub.Items.Data.FirstOrDefault()?.Price?.Id;

        if (string.IsNullOrEmpty(priceId))
        {
            throw new InvalidOperationException($"Price ID not found in subscription {stripeSub.Id}");
        }

        var plan = await _context.Plans
            .FirstOrDefaultAsync(
                p => (p.StripeMonthlyPriceId == priceId || p.StripeAnnualPriceId == priceId) && !p.IsDeleted,
                cancellationToken);

        if (plan == null)
        {
            throw new InvalidOperationException($"Plan not found for price {priceId}");
        }

        return plan;
    }

    private static SubscriptionStatus MapStripeStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "trialing" => SubscriptionStatus.Trialing,
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Cancelled,
            "unpaid" => SubscriptionStatus.Unpaid,
            "incomplete" => SubscriptionStatus.Incomplete,
            "incomplete_expired" => SubscriptionStatus.Cancelled,
            "paused" => SubscriptionStatus.Paused,
            _ => SubscriptionStatus.Incomplete,
        };
    }

    private static BillingInterval GetBillingInterval(global::Stripe.Subscription stripeSub)
    {
        var interval = stripeSub.Items.Data.FirstOrDefault()?.Price?.Recurring?.Interval;

        return interval switch
        {
            "year" => BillingInterval.Annual,
            _ => BillingInterval.Monthly,
        };
    }
}
