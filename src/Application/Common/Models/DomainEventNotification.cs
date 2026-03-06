using Application.Domain.Common;
﻿namespace Application.Common.Models;

public class DomainEventNotification<TDomainEvent> : INotification
    where TDomainEvent : BaseEvent
{
    public DomainEventNotification(TDomainEvent domainEvent)
    {
        DomainEvent = domainEvent;
    }

    public TDomainEvent DomainEvent { get; }
}