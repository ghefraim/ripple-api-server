using Application.Common.Interfaces;
using Application.Common.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Stripe;
using Stripe.Checkout;

namespace Application.Infrastructure.Services;

public class StripeService : IStripeService
{
    private readonly ILogger<StripeService> _logger;
    private readonly StripeOptions _stripeOptions;

    public StripeService(
        IOptions<StripeOptions> stripeOptions,
        ILogger<StripeService> logger)
    {
        _stripeOptions = stripeOptions.Value;
        _logger = logger;

        StripeConfiguration.ApiKey = _stripeOptions.SecretKey;
    }

    public async Task<Session> CreateCheckoutSessionAsync(
        string customerId,
        string priceId,
        string successUrl,
        string cancelUrl,
        Dictionary<string, string>? metadata = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var options = new SessionCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1,
                },
            ],
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = metadata,
            // Pass metadata to the subscription so webhooks can identify the organization
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = metadata,
            },
        };

        var requestOptions = CreateRequestOptions(idempotencyKey);
        var service = new SessionService();

        _logger.LogInformation("Creating checkout session for customer {CustomerId}", customerId);

        return await service.CreateAsync(options, requestOptions, cancellationToken);
    }

    public async Task<Stripe.BillingPortal.Session> CreateBillingPortalSessionAsync(
        string customerId,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl,
        };

        var service = new Stripe.BillingPortal.SessionService();

        _logger.LogInformation("Creating billing portal session for customer {CustomerId}", customerId);

        return await service.CreateAsync(options, cancellationToken: cancellationToken);
    }

    public async Task<Customer> CreateCustomerAsync(
        string email,
        string? name = null,
        Dictionary<string, string>? metadata = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var options = new CustomerCreateOptions
        {
            Email = email,
            Name = name,
            Metadata = metadata,
        };

        var requestOptions = CreateRequestOptions(idempotencyKey);
        var service = new CustomerService();

        _logger.LogInformation("Creating Stripe customer for email {Email}", email);

        return await service.CreateAsync(options, requestOptions, cancellationToken);
    }

    public async Task<Customer> GetCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var service = new CustomerService();
        return await service.GetAsync(customerId, cancellationToken: cancellationToken);
    }

    public async Task<Stripe.Subscription> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var service = new Stripe.SubscriptionService();
        return await service.GetAsync(subscriptionId, cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<Stripe.Subscription>> ListSubscriptionsAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var service = new Stripe.SubscriptionService();
        var options = new Stripe.SubscriptionListOptions
        {
            Customer = customerId,
            Status = "all",
            Expand = ["data.items.data.price"],
        };

        var subscriptions = await service.ListAsync(options, cancellationToken: cancellationToken);
        return subscriptions.Data;
    }

    public async Task<Stripe.Subscription> CancelSubscriptionAsync(
        string subscriptionId,
        bool cancelAtPeriodEnd = true,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var service = new Stripe.SubscriptionService();

        if (cancelAtPeriodEnd)
        {
            var options = new Stripe.SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true,
            };

            var requestOptions = CreateRequestOptions(idempotencyKey);

            _logger.LogInformation("Cancelling subscription {SubscriptionId} at period end", subscriptionId);

            return await service.UpdateAsync(subscriptionId, options, requestOptions, cancellationToken);
        }

        _logger.LogInformation("Cancelling subscription {SubscriptionId} immediately", subscriptionId);

        return await service.CancelAsync(subscriptionId, cancellationToken: cancellationToken);
    }

    public Event ConstructEvent(string payload, string signature, string webhookSecret)
    {
        // Allow API version mismatches between Stripe CLI and Stripe.net SDK
        // This is safe because we access event data generically and the core fields are stable
        return EventUtility.ConstructEvent(payload, signature, webhookSecret, throwOnApiVersionMismatch: false);
    }

    private static RequestOptions? CreateRequestOptions(string? idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return null;
        }

        return new RequestOptions
        {
            IdempotencyKey = idempotencyKey,
        };
    }
}
