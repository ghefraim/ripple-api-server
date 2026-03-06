using Application.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Application.Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChangesInterceptor for publishing domain events during SaveChanges
/// </summary>
public class DispatchDomainEventsInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;

    public DispatchDomainEventsInterceptor(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Intercept SaveChanges to publish domain events after successful save
    /// </summary>
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PublishDomainEvents(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    /// <summary>
    /// Intercept SaveChangesAsync to publish domain events after successful save
    /// </summary>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await PublishDomainEvents(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Collect and publish domain events from all entities
    /// </summary>
    /// <param name="context">The database context</param>
    private async Task PublishDomainEvents(DbContext? context)
    {
        if (context == null) return;

        var entities = context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // Clear events from entities to prevent re-publishing
        entities.ForEach(e => e.ClearDomainEvents());

        // Publish each domain event
        var tasks = domainEvents
            .Select(domainEvent => _mediator.Publish(domainEvent));

        await Task.WhenAll(tasks);
    }
}