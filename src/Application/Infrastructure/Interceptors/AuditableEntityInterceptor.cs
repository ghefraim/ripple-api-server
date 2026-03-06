using Application.Common.Helpers;
using Application.Common.Interfaces;
using Application.Domain.Common;
using Application.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Application.Infrastructure.Interceptors;

/// <summary>
/// EF Core SaveChangesInterceptor for automatic audit processing of AuditableEntity changes
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTimeService;

    public AuditableEntityInterceptor(
        ICurrentUserService currentUserService,
        IDateTime dateTimeService)
    {
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    /// <summary>
    /// Intercept SaveChanges to update audit metadata and create audit entries
    /// </summary>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <summary>
    /// Intercept SaveChangesAsync to update audit metadata and create audit entries
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Update audit metadata and create audit entries for all changed entities
    /// </summary>
    /// <param name="context">The database context</param>
    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        var userId = _currentUserService.UserId ?? "system";
        var userEmail = _currentUserService.UserEmail;
        var timestamp = _dateTimeService.Now;
        var organizationId = _currentUserService.OrganizationId;

        // Process AuditableEntity instances for metadata updates
        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.CreatedOn = timestamp;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedBy = userId;
                    entry.Entity.UpdatedOn = timestamp;
                    break;
            }
        }

        // Create audit entries for all BaseEntity instances (includes AuditableEntity)
        var auditEntries = new List<AuditEntry>();

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = CreateAuditEntry(entry, userId, userEmail, timestamp, organizationId);
            if (auditEntry != null)
            {
                auditEntries.Add(auditEntry);
            }
        }

        // Add audit entries to context if any were created
        if (auditEntries.Any())
        {
            context.Set<AuditEntry>().AddRange(auditEntries);
        }
    }

    /// <summary>
    /// Create an audit entry for the given entity entry
    /// </summary>
    /// <param name="entry">The entity entry</param>
    /// <param name="userId">The current user ID</param>
    /// <param name="userEmail">The current user email</param>
    /// <param name="timestamp">The timestamp of the change</param>
    /// <param name="organizationId">The current organization ID</param>
    /// <returns>An audit entry or null if no changes to audit</returns>
    private static AuditEntry? CreateAuditEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseEntity> entry,
        string userId,
        string userEmail,
        DateTime timestamp,
        Guid? organizationId)
    {
        var entityName = entry.Entity.GetType().Name;
        var entityId = entry.Entity.Id.ToString();

        // Determine the action
        var action = entry.State switch
        {
            EntityState.Added => "Added",
            EntityState.Modified => "Modified",
            EntityState.Deleted => "Deleted",
            _ => null
        };

        if (action == null) return null;

        // Get property changes
        var changes = AuditHelper.GetChangedProperties(entry);

        // Skip if no meaningful changes (shouldn't happen, but safety check)
        if (!changes.Any() && entry.State != EntityState.Added && entry.State != EntityState.Deleted)
            return null;

        var auditEntry = new AuditEntry
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            Timestamp = timestamp,
            UserId = userId,
            UserEmail = userEmail,
            OrganizationId = organizationId ?? Guid.Empty
        };

        auditEntry.SetDetails(changes);

        return auditEntry;
    }
}