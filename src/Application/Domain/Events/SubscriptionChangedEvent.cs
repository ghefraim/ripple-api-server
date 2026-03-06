using Application.Domain.Common;
using Application.Domain.Entities;

namespace Application.Domain.Events;

public sealed class SubscriptionChangedEvent(Subscription subscription) : BaseEvent
{
    public Subscription Subscription { get; } = subscription;
}
