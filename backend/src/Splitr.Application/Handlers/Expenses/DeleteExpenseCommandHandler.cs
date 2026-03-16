using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Commands.Expenses;
using Splitr.Application.Exceptions;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Expenses;

public class DeleteExpenseCommandHandler(IAppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<DeleteExpenseCommand, Unit>
{
    public async Task<Unit> Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await dbContext.Expenses.FirstOrDefaultAsync(
            e => e.Id == request.ExpenseId && e.GroupId == request.GroupId && e.DeletedAt == null,
            cancellationToken
        ) ?? throw new InvalidOperationException("Expense not found.");

        var isAdmin = await dbContext.GroupMembers.AnyAsync(
            gm => gm.GroupId == request.GroupId && gm.UserId == currentUser.UserId && gm.Role == GroupRole.Admin,
            cancellationToken
        );

        if (expense.PaidBy != currentUser.UserId && !isAdmin)
            throw new ForbiddenAccessException("Only the creator or a group admin can delete this expense.");

        var hasConfirmedSettlement = await dbContext.Settlements.AnyAsync(
            s => s.GroupId == request.GroupId && s.Status == SettlementStatus.Confirmed,
            cancellationToken
        );

        if (hasConfirmedSettlement)
            throw new InvalidOperationException("Cannot delete an expense when confirmed settlements exist in this group.");

        expense.DeletedAt = DateTime.UtcNow;

        // Auto-cancel pending settlements
        await dbContext.Settlements
            .Where(s => s.GroupId == request.GroupId && s.Status == SettlementStatus.Pending)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, SettlementStatus.Cancelled),
                cancellationToken
            );

        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.ExpenseDeleted, new { ExpenseId = expense.Id, expense.GroupId }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
