using Splitr.Application.Mediator;
using Splitr.Application.Commands.Expenses;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Expenses;

public class AddExpenseCommandHandler(IAppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<AddExpenseCommand, Guid>
{
    public async Task<Guid> Handle(AddExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = new Expense
        {
            GroupId = request.GroupId,
            PaidBy = currentUser.UserId,
            AmountPaise = request.AmountPaise,
            Description = request.Description,
            SplitType = request.SplitType,
            Splits = request.Splits.Select(s => new ExpenseSplit
            {
                UserId = s.UserId,
                AmountPaise = s.AmountPaise
            }).ToList()
        };

        dbContext.Expenses.Add(expense);
        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.ExpenseAdded, new { expense.Id, expense.GroupId, expense.PaidBy, expense.AmountPaise }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return expense.Id;
    }
}
