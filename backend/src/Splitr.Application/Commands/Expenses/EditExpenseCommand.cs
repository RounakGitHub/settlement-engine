using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;
using Splitr.Domain.Enums;

namespace Splitr.Application.Commands.Expenses;

public record EditExpenseCommand(Guid GroupId, Guid ExpenseId, long AmountPaise, string Description, SplitType SplitType, List<ExpenseSplitDto> Splits) : IRequest<Unit>, IRequireGroupMembership;
