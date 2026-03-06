using Application.Common.Interfaces;
using Application.Domain.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Application.Infrastructure.Interceptors;

public class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly IDateTime _dateTimeService;

    public SoftDeleteInterceptor(IDateTime dateTimeService)
    {
        _dateTimeService = dateTimeService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ProcessSoftDeletes(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ProcessSoftDeletes(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessSoftDeletes(DbContext? context)
    {
        if (context == null) return;

        var deletedEntities = context.ChangeTracker
            .Entries()
            .Where(entry => entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable)
            .ToList();

        foreach (var entry in deletedEntities)
        {
            entry.State = EntityState.Unchanged;

            var softDeletableEntity = (ISoftDeletable)entry.Entity;
            softDeletableEntity.IsDeleted = true;
            softDeletableEntity.DeletedOn = _dateTimeService.Now;
        }
    }
}