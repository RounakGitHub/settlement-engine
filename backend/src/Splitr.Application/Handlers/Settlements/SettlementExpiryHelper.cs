using Microsoft.EntityFrameworkCore;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Settlements;

/// <summary>
/// Lazily expires pending settlements past their ExpiresAt timestamp.
/// Called on read paths so stale settlements are transitioned on access.
/// </summary>
public static class SettlementExpiryHelper
{
    public static async Task ExpireStaleSettlements(IAppDbContext dbContext, Guid groupId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var stale = await dbContext.Settlements
            .Where(s => s.GroupId == groupId && s.Status == SettlementStatus.Pending && s.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
            return;

        await dbContext.Settlements
            .Where(s => s.GroupId == groupId && s.Status == SettlementStatus.Pending && s.ExpiresAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SettlementStatus.Expired), cancellationToken);

        var outboxEvents = stale.Select(s => OutboxEvent.From(EventType.SettlementExpired, new { s.Id, s.GroupId }));

        dbContext.OutboxEvents.AddRange(outboxEvents);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
