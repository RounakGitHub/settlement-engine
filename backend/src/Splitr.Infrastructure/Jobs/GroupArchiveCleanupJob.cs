using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Splitr.Infrastructure.Persistence;

namespace Splitr.Infrastructure.Jobs;

public sealed class GroupArchiveCleanupJob(
    IServiceScopeFactory scopeFactory,
    ILogger<GroupArchiveCleanupJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

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

                var deleted = await dbContext.Groups
                    .Where(g => g.IsArchived && g.DeleteAfter != null && g.DeleteAfter <= now)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                    logger.LogInformation("Cleaned up {Count} archived groups", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Group archive cleanup job failed");
            }
        }
    }
}
