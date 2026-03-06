using Application.Common.Interfaces;
using Application.Common.Security;
using Application.Domain.Enums;

using MediatR;

namespace Application.Features.Billing.GetCurrentSubscription;

[Authorize]
public record GetCurrentSubscriptionQuery : IRequest<CurrentSubscriptionResponse?>;

public record CurrentSubscriptionResponse(
    Guid Id,
    Guid PlanId,
    string PlanName,
    SubscriptionStatus Status,
    BillingInterval BillingInterval,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    bool CancelAtPeriodEnd,
    DateTime? CancelledAt,
    DateTime? TrialStart,
    DateTime? TrialEnd,
    List<SubscriptionFeatureResponse> Features
);

public record SubscriptionFeatureResponse(
    string FeatureKey,
    string FeatureType,
    string Value
);

public class GetCurrentSubscriptionQueryHandler(
    ISubscriptionService subscriptionService,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionResponse?>
{
    private readonly ISubscriptionService _subscriptionService = subscriptionService;
    private readonly ICurrentUserService _currentUserService = currentUserService;

    public async Task<CurrentSubscriptionResponse?> Handle(GetCurrentSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var organizationId = _currentUserService.OrganizationId
            ?? throw new UnauthorizedAccessException("No organization selected.");

        var subscription = await _subscriptionService.GetCurrentSubscriptionAsync(organizationId, cancellationToken);

        if (subscription == null)
        {
            return null;
        }

        return new CurrentSubscriptionResponse(
            subscription.Id,
            subscription.PlanId,
            subscription.Plan.Name,
            subscription.Status,
            subscription.BillingInterval,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.CancelAtPeriodEnd,
            subscription.CancelledAt,
            subscription.TrialStart,
            subscription.TrialEnd,
            subscription.Plan.Features.Where(f => !f.IsDeleted).Select(f => new SubscriptionFeatureResponse(
                f.FeatureKey,
                f.FeatureType,
                f.Value
            )).ToList()
        );
    }
}
