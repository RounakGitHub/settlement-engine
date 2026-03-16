using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Splitr.Application.Interfaces;
using Splitr.Infrastructure.Configuration;
using Splitr.Infrastructure.Messaging;

namespace Splitr.Infrastructure.Consumers;

public class SignalRDispatcherConsumer(IOptions<KafkaOptions> kafkaOptions, ISignalRDispatcher dispatcher, ILogger<SignalRDispatcherConsumer> logger) : KafkaConsumerService(kafkaOptions, logger)
{
    protected override IReadOnlyList<string> Topics => Kafka.GetAllTopics();
    protected override string ConsumerGroupId => Kafka.Consumers.SignalRDispatcherGroupId;

    protected override async Task ProcessMessageAsync(string key, string value, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(value);
        var root = doc.RootElement;

        if (!root.TryGetProperty("EventType", out var eventTypeProp))
            return;

        var eventType = eventTypeProp.GetString()!;

        if (!root.TryGetProperty("Data", out var data) || !data.TryGetProperty("GroupId", out var groupIdProp))
            return;

        var groupId = groupIdProp.GetGuid();

        await dispatcher.DispatchAsync(groupId, eventType, value, ct);

        logger.LogDebug("Dispatched {EventType} to SignalR group {GroupId}", eventType, groupId);
    }
}
