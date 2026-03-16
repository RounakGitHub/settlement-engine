using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Splitr.Infrastructure.Configuration;
using Splitr.Infrastructure.Persistence;

namespace Splitr.Infrastructure.Messaging;

/// <summary>
/// Background service that publishes outbox events to Kafka.
/// On startup: sweeps all unpublished events (crash recovery).
/// At runtime: awaits signals from OutboxChannel for near-instant publishing.
/// </summary>
public class OutboxPublisherService(
    OutboxChannel channel,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> kafkaOptions,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private readonly KafkaOptions _options = kafkaOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PublishPendingEvents(stoppingToken);

        while (await channel.Reader.WaitToReadAsync(stoppingToken))
        {
            while (channel.Reader.TryRead(out _)) { }

            try
            {
                await PublishPendingEvents(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox events");
                using var timer = new PeriodicTimer(RetryInterval);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
    }

    private async Task PublishPendingEvents(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var kafka = scope.ServiceProvider.GetRequiredService<KafkaProducerService>();

        while (!ct.IsCancellationRequested)
        {
            var evt = await dbContext.OutboxEvents
                .Where(e => e.PublishedAt == null)
                .OrderBy(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (evt is null)
                break;

            try
            {
                var topic = _options.GetTopicForEvent(evt.EventType);
                var envelope = JsonSerializer.Serialize(new { evt.EventType, Data = JsonSerializer.Deserialize<JsonElement>(evt.Payload) });
                await kafka.ProduceAsync(topic, evt.Id.ToString(), envelope);

                evt.PublishedAt = DateTime.UtcNow;
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE outbox_events SET published_at = {evt.PublishedAt} WHERE id = {evt.Id}",
                    ct
                );

                logger.LogDebug("Published outbox event {EventId} ({EventType}) to {Topic}",
                    evt.Id, evt.EventType, topic);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox event {EventId}", evt.Id);
                return;
            }
        }
    }
}