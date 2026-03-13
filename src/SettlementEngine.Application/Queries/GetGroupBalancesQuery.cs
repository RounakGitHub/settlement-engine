using MediatR;

namespace SettlementEngine.Application.Queries;

public sealed record GetGroupBalancesQuery(Guid GroupId) : IRequest<GroupBalancesResult>;

public sealed record GroupBalancesResult(
    Guid GroupId,
    IReadOnlyList<MemberBalance> Balances
);

public sealed record MemberBalance(
    Guid MemberId,
    decimal NetBalance,
    IReadOnlyList<DebtEntry> Owes
);

public sealed record DebtEntry(Guid ToUserId, decimal Amount);
