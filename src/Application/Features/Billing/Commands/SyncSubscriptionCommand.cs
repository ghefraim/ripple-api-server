using Application.Common.Interfaces;
using Application.Common.Security;

using MediatR;

namespace Application.Features.Billing.SyncSubscription;

[Authorize]
public record SyncSubscriptionCommand : IRequest<SyncSubscriptionResponse>;

public record SyncSubscriptionResponse(
    bool Synced,
    string? Message
);

public class SyncSubscriptionCommandHandler(
    ISubscriptionService subscriptionService,
    ICurrentUserService currentUserService)
    : IRequestHandler<SyncSubscriptionCommand, SyncSubscriptionResponse>
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<SyncSubscriptionResponse> Handle(SyncSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var subscription = await _subscriptionService.SyncFromStripeByOrganizationAsync(organizationId, cancellationToken);

        if (subscription == null)
        {
            return new SyncSubscriptionResponse(false, "No active subscription found in Stripe.");
        }

        return new SyncSubscriptionResponse(true, $"Subscription synced: {subscription.Status}");
    }
}
