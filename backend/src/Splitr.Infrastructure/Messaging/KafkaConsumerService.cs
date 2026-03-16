using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Splitr.Infrastructure.Configuration;

namespace Splitr.Infrastructure.Messaging;

public abstract class KafkaConsumerService(
    IOptions<KafkaOptions> kafkaOptions,
    ILogger logger) : BackgroundService
{
    protected KafkaOptions Kafka { get; } = kafkaOptions.Value;
    protected abstract IReadOnlyList<string> Topics { get; }
    protected abstract string ConsumerGroupId { get; }

    protected abstract Task ProcessMessageAsync(string key, string value, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = Kafka.BootstrapServers,
            GroupId = ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topics);

        logger.LogInformation("Consuming topics [{Topics}] with group {GroupId}", Topics, ConsumerGroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
                    if (result is null)
                        continue;

                    await ProcessMessageAsync(result.Message.Key, result.Message.Value, stoppingToken);

                    consumer.Commit(result);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Consume error in group {GroupId}", ConsumerGroupId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Processing error in group {GroupId}", ConsumerGroupId);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
