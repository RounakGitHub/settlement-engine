using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;

namespace Splitr.Application.Commands.Expenses;

public record DeleteExpenseCommand(Guid GroupId, Guid ExpenseId) : IRequest<Unit>, IRequireGroupMembership;
