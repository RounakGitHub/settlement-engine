using SettlementEngine.Domain.Events;

namespace SettlementEngine.Application.Interfaces;

public interface IOutboxRepository
{
    Task AddAsync(IDomainEvent @event, string topic, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxEntry>> GetUnpublishedAsync(int batchSize, CancellationToken ct = default);
    Task MarkPublishedAsync(Guid eventId, CancellationToken ct = default);
}

public sealed record OutboxEntry(
    Guid EventId,
    string Topic,
    string Payload,
    DateTime CreatedAt,
    int RetryCount
);
