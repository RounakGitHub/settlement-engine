namespace SettlementEngine.Domain.Events;

public sealed record DebtGraphUpdated(
    Guid EventId,
    Guid GroupId,
    long Version,
    IReadOnlyList<SimplifiedTransfer> Transfers,
    DateTime ComputedAt
) : IDomainEvent;

public sealed record SimplifiedTransfer(
    Guid FromUserId,
    Guid ToUserId,
    decimal Amount
);
