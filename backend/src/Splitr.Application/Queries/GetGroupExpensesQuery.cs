using Splitr.Application.Mediator;
using Splitr.Application.Behaviours;
using Splitr.Domain.Enums;

namespace Splitr.Application.Queries;

public record ExpenseDto(Guid Id, Guid PaidBy, string PaidByName, long AmountPaise, string Description, SplitType SplitType, DateTime CreatedAt);

public record GetGroupExpensesQuery(Guid GroupId) : IRequest<List<ExpenseDto>>, IRequireGroupMembership;
