using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Infrastructure.Persistence;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace Application.Features.Billing.CancelSubscription;

[Authorize(Roles = "Owner")]
public record CancelSubscriptionCommand(
    bool CancelImmediately = false
) : IRequest<CancelSubscriptionResponse>;

public record CancelSubscriptionResponse(
    bool Success,
    string Message
);

public class CancelSubscriptionCommandHandler(
    ApplicationDbContext context,
    IStripeService stripeService,
    ISubscriptionService subscriptionService,
    ICurrentUserService currentUserService)
    : IRequestHandler<CancelSubscriptionCommand, CancelSubscriptionResponse>
{
    private readonly ApplicationDbContext _context = context;
    private readonly IStripeService _stripeService = stripeService;
    private readonly ISubscriptionService _subscriptionService = subscriptionService;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<CancelSubscriptionResponse> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var subscription = await _context.Subscriptions
            .IgnoreQueryFilters()
            .Where(s => s.OrganizationId == organizationId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedOn)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No active subscription found.");

        if (string.IsNullOrEmpty(subscription.StripeSubscriptionId))
        {
            throw new InvalidOperationException("No Stripe subscription ID found.");
        }

        var idempotencyKey = $"cancel_{subscription.StripeSubscriptionId}_{DateTime.UtcNow:yyyyMMddHHmm}";

        var cancelledSubscription = await _stripeService.CancelSubscriptionAsync(
            subscription.StripeSubscriptionId,
            !request.CancelImmediately,
            idempotencyKey,
            cancellationToken);

        // Sync immediately so we don't rely on webhooks
        cancelledSubscription.Metadata["organizationId"] = organizationId.ToString();
        await _subscriptionService.SyncFromStripeAsync(cancelledSubscription, cancellationToken);

        var message = request.CancelImmediately
            ? "Your subscription has been cancelled immediately."
            : "Your subscription will be cancelled at the end of the current billing period.";

        return new CancelSubscriptionResponse(true, message);
    }
}
