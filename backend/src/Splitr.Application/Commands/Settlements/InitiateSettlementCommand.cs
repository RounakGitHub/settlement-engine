using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;

namespace Splitr.Application.Commands.Settlements;

public record InitiateSettlementResult(Guid SettlementId, string RazorpayOrderId);

public record InitiateSettlementCommand(Guid GroupId, Guid PayeeId, long AmountPaise) : IRequest<InitiateSettlementResult>, IRequireGroupMembership;
