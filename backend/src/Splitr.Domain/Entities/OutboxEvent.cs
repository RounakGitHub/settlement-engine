using System.Text.Json;
using Splitr.Domain.Enums;

namespace Splitr.Domain.Entities;

public class OutboxEvent : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }

    public static OutboxEvent From(EventType eventType, object payload) => new()
    {
        EventType = eventType.ToString(),
        Payload = JsonSerializer.Serialize(payload)
    };
}
