using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Splitr.Infrastructure.Messaging;

public class KafkaProducerService(
    IProducer<string, string> producer,
    ILogger<KafkaProducerService> logger) : IDisposable
{
    public async Task ProduceAsync(string topic, string key, string value)
    {
        try
        {
            var result = await producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = value
            });

            logger.LogDebug("Produced message to {Topic} [{Partition}] @ offset {Offset}",
                topic, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            logger.LogError(ex, "Failed to produce message to {Topic} with key {Key}", topic, key);
            throw;
        }
    }

    public void Dispose()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
    }
}
