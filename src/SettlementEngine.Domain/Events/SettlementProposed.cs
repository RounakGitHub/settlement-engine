namespace SettlementEngine.Domain.Events;

public sealed record SettlementProposed(
    Guid EventId,
    Guid GroupId,
    Guid SettlementId,
    Guid FromUserId,
    Guid ToUserId,
    decimal Amount,
    string? Note,
    DateTime ExpiresAt,
    DateTime OccurredAt
) : IDomainEvent;
