using MediatR;

namespace SettlementEngine.Application.Commands;

public sealed record AddExpenseCommand(
    Guid GroupId,
    Guid PaidByUserId,
    decimal Amount,
    string Description,
    IReadOnlyList<Guid> SplitAmongUserIds,
    string SplitType,
    string IdempotencyKey
) : IRequest<AddExpenseResult>;

public sealed record AddExpenseResult(Guid ExpenseEventId, bool WasIdempotent);
