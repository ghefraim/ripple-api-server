namespace Application.Common.Interfaces;

public interface ISubscriptionService
{
    Task<Domain.Entities.Subscription?> GetCurrentSubscriptionAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<Domain.Entities.Subscription> SyncFromStripeAsync(Stripe.Subscription stripeSubscription, CancellationToken cancellationToken = default);

    Task<Domain.Entities.Subscription?> SyncFromStripeByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<Domain.Entities.BillingCustomer> GetOrCreateBillingCustomerAsync(Guid organizationId, string email, string? name = null, CancellationToken cancellationToken = default);
}
