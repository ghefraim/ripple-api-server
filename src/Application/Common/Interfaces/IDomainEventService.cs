using Application.Domain.Common;
﻿namespace Application.Common.Interfaces;

public interface IDomainEventService
{
    Task Publish(BaseEvent domainEvent);
}