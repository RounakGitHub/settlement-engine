using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;
using Splitr.Domain.Enums;

namespace Splitr.Application.Commands.Expenses;

public record ExpenseSplitDto(Guid UserId, long AmountPaise);

public record AddExpenseCommand(Guid GroupId, long AmountPaise, string Description, SplitType SplitType, List<ExpenseSplitDto> Splits) : IRequest<Guid>, IRequireGroupMembership;
