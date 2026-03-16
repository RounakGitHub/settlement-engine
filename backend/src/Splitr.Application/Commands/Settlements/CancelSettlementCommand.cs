using Splitr.Application.Mediator;

namespace Splitr.Application.Commands.Settlements;

public record CancelSettlementCommand(Guid SettlementId) : IRequest<Unit>;
