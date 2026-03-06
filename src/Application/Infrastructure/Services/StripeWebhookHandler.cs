using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Domain.Entities;
using Application.Domain.Enums;
using Application.Domain.Events;
using Application.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Stripe;

namespace Application.Infrastructure.Services;

public class StripeWebhookHandler : IStripeWebhookHandler
{
    private readonly ApplicationDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IEntitlementService _entitlementService;
    private readonly ILogger<StripeWebhookHandler> _logger;
    private readonly StripeOptions _stripeOptions;

    public StripeWebhookHandler(
        ApplicationDbContext context,
        IStripeService stripeService,
        ISubscriptionService subscriptionService,
        IEntitlementService entitlementService,
        IOptions<StripeOptions> stripeOptions,
        ILogger<StripeWebhookHandler> logger)
    {
        _context = context;
        _stripeService = stripeService;
        _subscriptionService = subscriptionService;
        _entitlementService = entitlementService;
        _stripeOptions = stripeOptions.Value;
        _logger = logger;
    }

    public async Task HandleAsync(string payload, string signature, CancellationToken cancellationToken = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = _stripeService.ConstructEvent(payload, signature, _stripeOptions.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Failed to construct Stripe event from webhook payload");
            throw;
        }

        var existingLog = await _context.StripeEventLogs
            .FirstOrDefaultAsync(e => e.StripeEventId == stripeEvent.Id, cancellationToken);

        if (existingLog != null)
        {
            _logger.LogInformation("Duplicate webhook event {EventId}, skipping", stripeEvent.Id);
            return;
        }

        var eventLog = new StripeEventLog
        {
            StripeEventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            Payload = payload,
            Status = StripeEventStatus.Received,
            ReceivedAt = DateTime.UtcNow,
        };

        _context.StripeEventLogs.Add(eventLog);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            eventLog.Status = StripeEventStatus.Processing;
            await _context.SaveChangesAsync(cancellationToken);

            await ProcessEventAsync(stripeEvent, cancellationToken);

            eventLog.Status = StripeEventStatus.Processed;
            eventLog.ProcessedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            eventLog.Status = StripeEventStatus.Failed;
            eventLog.ErrorMessage = ex.Message;
            eventLog.RetryCount++;

            _logger.LogError(ex, "Failed to process Stripe event {EventId} of type {EventType}",
                stripeEvent.Id, stripeEvent.Type);

            throw;
        }
        finally
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessEventAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing Stripe event {EventId} of type {EventType}",
            stripeEvent.Id, stripeEvent.Type);

        switch (stripeEvent.Type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                await HandleSubscriptionChangedAsync(stripeEvent, cancellationToken);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent, cancellationToken);
                break;

            case "checkout.session.completed":
                await HandleCheckoutCompletedAsync(stripeEvent, cancellationToken);
                break;

            default:
                _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleSubscriptionChangedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Stripe.Subscription stripeSub)
        {
            _logger.LogWarning("Invalid subscription object in event {EventId}", stripeEvent.Id);
            return;
        }

        var subscription = await _subscriptionService.SyncFromStripeAsync(stripeSub, cancellationToken);

        if (subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.Trialing)
        {
            await _entitlementService.RevokeAllFromSourceAsync(
                subscription.OrganizationId,
                EntitlementSource.Plan,
                subscription.PlanId,
                cancellationToken);

            await _entitlementService.ProvisionFromPlanAsync(
                subscription.OrganizationId,
                subscription.PlanId,
                cancellationToken);
        }

        subscription.AddDomainEvent(new SubscriptionChangedEvent(subscription));
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Subscription {SubscriptionId} changed to status {Status}",
            stripeSub.Id, subscription.Status);
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Stripe.Subscription stripeSub)
        {
            _logger.LogWarning("Invalid subscription object in event {EventId}", stripeEvent.Id);
            return;
        }

        var subscription = await _subscriptionService.SyncFromStripeAsync(stripeSub, cancellationToken);

        await _entitlementService.RevokeAllFromSourceAsync(
            subscription.OrganizationId,
            EntitlementSource.Plan,
            subscription.PlanId,
            cancellationToken);

        subscription.AddDomainEvent(new SubscriptionChangedEvent(subscription));
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Subscription {SubscriptionId} deleted", stripeSub.Id);
    }

    private async Task HandleCheckoutCompletedAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        if (stripeEvent.Data.Object is not Stripe.Checkout.Session session)
        {
            _logger.LogWarning("Invalid checkout session object in event {EventId}", stripeEvent.Id);
            return;
        }

        if (session.Mode != "subscription" || string.IsNullOrEmpty(session.SubscriptionId))
        {
            _logger.LogInformation("Checkout session {SessionId} is not a subscription, skipping", session.Id);
            return;
        }

        var stripeSub = await _stripeService.GetSubscriptionAsync(session.SubscriptionId, cancellationToken);
        await HandleSubscriptionChangedAsync(
            new Event { Id = stripeEvent.Id, Type = "customer.subscription.created", Data = new EventData { Object = stripeSub } },
            cancellationToken);

        _logger.LogInformation("Checkout session {SessionId} completed", session.Id);
    }
}
