using SettlementEngine.Domain.Events;

namespace SettlementEngine.Application.Interfaces;

public interface IEventStore
{
    Task AppendAsync(IDomainEvent @event, CancellationToken ct = default);
    Task<IReadOnlyList<IDomainEvent>> GetEventsAsync(Guid groupId, CancellationToken ct = default);
}
