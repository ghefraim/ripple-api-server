using System.ComponentModel.DataAnnotations.Schema;

namespace Application.Domain.Common;

/// <summary>
/// Base entity class with domain events support.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }

    private readonly List<BaseEvent> _domainEvents = new();

    /// <summary>
    /// Collection of domain events for this entity.
    /// </summary>
    [NotMapped]
    public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Add a domain event to this entity.
    /// </summary>
    /// <param name="domainEvent">The domain event to add</param>
    public void AddDomainEvent(BaseEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Remove a domain event from this entity.
    /// </summary>
    /// <param name="domainEvent">The domain event to remove</param>
    public void RemoveDomainEvent(BaseEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Clear all domain events from this entity.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}