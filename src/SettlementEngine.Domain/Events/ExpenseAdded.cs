namespace SettlementEngine.Domain.Events;

public sealed record ExpenseAdded(
    Guid EventId,
    Guid GroupId,
    Guid PaidByUserId,
    decimal Amount,
    string Description,
    IReadOnlyList<Guid> SplitAmongUserIds,
    string SplitType,
    string IdempotencyKey,
    DateTime OccurredAt
) : IDomainEvent;
