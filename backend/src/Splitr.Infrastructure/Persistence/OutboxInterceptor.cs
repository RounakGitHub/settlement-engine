using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Splitr.Domain.Entities;
using Splitr.Infrastructure.Messaging;

namespace Splitr.Infrastructure.Persistence;

/// <summary>
/// EF Core interceptor that signals the OutboxChannel whenever new outbox events
/// are persisted, so the publisher picks them up immediately.
/// </summary>
public class OutboxInterceptor(OutboxChannel channel) : SaveChangesInterceptor
{
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        NotifyIfOutboxEvents(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        NotifyIfOutboxEvents(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void NotifyIfOutboxEvents(DbContext? context)
    {
        var hasOutboxEvents = context?.ChangeTracker
            .Entries<OutboxEvent>()
            .Any(e => e.State == EntityState.Unchanged) ?? false;

        if (hasOutboxEvents)
            channel.Signal();
    }
}
