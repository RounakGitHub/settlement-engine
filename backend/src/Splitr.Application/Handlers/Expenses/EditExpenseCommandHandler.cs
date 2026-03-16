using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Commands.Expenses;
using Splitr.Application.Exceptions;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Expenses;

public class EditExpenseCommandHandler(IAppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<EditExpenseCommand, Unit>
{
    public async Task<Unit> Handle(EditExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await dbContext.Expenses
            .Include(e => e.Splits)
            .FirstOrDefaultAsync(
                e => e.Id == request.ExpenseId && e.GroupId == request.GroupId && e.DeletedAt == null,
                cancellationToken
            )
            ?? throw new InvalidOperationException("Expense not found.");

        var isAdmin = await dbContext.GroupMembers.AnyAsync(
            gm => gm.GroupId == request.GroupId && gm.UserId == currentUser.UserId && gm.Role == GroupRole.Admin,
            cancellationToken
        );

        if (expense.PaidBy != currentUser.UserId && !isAdmin)
            throw new ForbiddenAccessException("Only the creator or a group admin can edit this expense.");

        // Snapshot old values for audit
        var oldSnapshot = new
        {
            expense.AmountPaise,
            expense.Description,
            expense.SplitType,
            Splits = expense.Splits.Select(s => new { s.UserId, s.AmountPaise }).ToList()
        };

        // Update expense
        expense.AmountPaise = request.AmountPaise;
        expense.Description = request.Description;
        expense.SplitType = request.SplitType;

        // Replace splits
        dbContext.ExpenseSplits.RemoveRange(expense.Splits);
        expense.Splits = request.Splits.Select(s => new ExpenseSplit
        {
            UserId = s.UserId,
            AmountPaise = s.AmountPaise
        }).ToList();

        // Auto-cancel pending settlements in this group
        await dbContext.Settlements
            .Where(s => s.GroupId == request.GroupId && s.Status == SettlementStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, SettlementStatus.Cancelled),
                cancellationToken
            );

        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.ExpenseEdited, new
        {
            ExpenseId = expense.Id,
            expense.GroupId,
            Old = oldSnapshot,
            New = new { request.AmountPaise, request.Description, request.SplitType, request.Splits }
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
