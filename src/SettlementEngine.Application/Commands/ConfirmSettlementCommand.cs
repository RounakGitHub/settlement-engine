using MediatR;

namespace SettlementEngine.Application.Commands;

public sealed record ConfirmSettlementCommand(
    Guid GroupId,
    Guid SettlementId,
    Guid ConfirmedByUserId
) : IRequest<ConfirmSettlementResult>;

public sealed record ConfirmSettlementResult(bool Success, string? Error);
