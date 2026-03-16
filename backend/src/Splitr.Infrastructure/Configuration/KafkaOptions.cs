namespace Splitr.Infrastructure.Configuration;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public required string BootstrapServers { get; init; }
    public required bool EnableIdempotence { get; init; }
    public required IReadOnlyDictionary<string, string[]> Topics { get; init; }
    public required KafkaConsumersOptions Consumers { get; init; }

    public string GetTopicForEvent(string eventType) => Topics.FirstOrDefault(kvp => kvp.Value.Contains(eventType)).Key ?? throw new InvalidOperationException($"No Kafka topic mapped for event type '{eventType}'");

    public IReadOnlyList<string> GetAllTopics() => Topics.Keys.ToList();

    public IReadOnlyList<string> GetTopicsForEvents(IEnumerable<string> eventTypes) => eventTypes.Select(GetTopicForEvent).Distinct().ToList();
}

public sealed class KafkaConsumersOptions
{
    public required string DebtSimplifierGroupId { get; init; }
    public required string SignalRDispatcherGroupId { get; init; }
    public required string EmailNotificationGroupId { get; init; }
}
