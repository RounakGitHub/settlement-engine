namespace SettlementEngine.Domain.Events;

public interface IDomainEvent
{
    Guid EventId { get; }
    Guid GroupId { get; }
    DateTime OccurredAt { get; }
}
