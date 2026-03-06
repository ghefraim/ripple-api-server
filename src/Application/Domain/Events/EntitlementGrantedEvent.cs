using Application.Domain.Common;
using Application.Domain.Entities;

namespace Application.Domain.Events;

public sealed class EntitlementGrantedEvent(Entitlement entitlement) : BaseEvent
{
    public Entitlement Entitlement { get; } = entitlement;
}
