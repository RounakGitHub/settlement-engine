using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Splitr.Domain.Entities;
using Splitr.Infrastructure.Persistence;
using Splitr.Domain.Enums;

namespace Splitr.Infrastructure.Jobs;

public sealed class SettlementExpiryJob(
    IServiceScopeFactory scopeFactory,
    ILogger<SettlementExpiryJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;

                var stale = await dbContext.Settlements
                    .Where(s => s.Status == SettlementStatus.Pending && s.ExpiresAt <= now)
                    .Select(s => new { s.Id, s.GroupId })
                    .ToListAsync(stoppingToken);

                if (stale.Count == 0)
                    continue;

                await dbContext.Settlements
                    .Where(s => s.Status == SettlementStatus.Pending && s.ExpiresAt <= now)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SettlementStatus.Expired), stoppingToken);

                var outboxEvents = stale.Select(s => OutboxEvent.From(EventType.SettlementExpired, new { s.Id, s.GroupId }));
                dbContext.OutboxEvents.AddRange(outboxEvents);
                await dbContext.SaveChangesAsync(stoppingToken);

                logger.LogInformation("Expired {Count} stale settlements", stale.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Settlement expiry job failed");
            }
        }
    }
}
