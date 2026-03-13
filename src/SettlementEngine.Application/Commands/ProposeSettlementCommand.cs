using MediatR;

namespace SettlementEngine.Application.Commands;

public sealed record ProposeSettlementCommand(
    Guid GroupId,
    Guid FromUserId,
    Guid ToUserId,
    decimal Amount,
    string? Note
) : IRequest<ProposeSettlementResult>;

public sealed record ProposeSettlementResult(Guid SettlementId);
