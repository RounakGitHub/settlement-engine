namespace SettlementEngine.Domain.Events;

public sealed record SettlementConfirmed(
    Guid EventId,
    Guid GroupId,
    Guid SettlementId,
    Guid ConfirmedByUserId,
    DateTime OccurredAt
) : IDomainEvent;
