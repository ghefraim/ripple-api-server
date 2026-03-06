using Stripe;
using Stripe.Checkout;

namespace Application.Common.Interfaces;

public interface IStripeService
{
    Task<Session> CreateCheckoutSessionAsync(
        string customerId,
        string priceId,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string>? metadata = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<Stripe.BillingPortal.Session> CreateBillingPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default);

    Task<Customer> CreateCustomerAsync(
        string email,
        string? name = null,
        Dictionary<string, string>? metadata = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Task<Customer> GetCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<Stripe.Subscription> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Stripe.Subscription>> ListSubscriptionsAsync(
        string customerId,
        CancellationToken cancellationToken = default);

    Task<Stripe.Subscription> CancelSubscriptionAsync(
        string subscriptionId,
        bool cancelAtPeriodEnd = true,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    Event ConstructEvent(string payload, string signature, string webhookSecret);
}
