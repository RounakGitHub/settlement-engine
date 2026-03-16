using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Interfaces;
using Splitr.Application.Queries;

namespace Splitr.Application.Handlers.Queries;

public class GetGroupExpensesQueryHandler(IAppDbContext dbContext) : IRequestHandler<GetGroupExpensesQuery, List<ExpenseDto>>
{
    public async Task<List<ExpenseDto>> Handle(GetGroupExpensesQuery request, CancellationToken cancellationToken)
    {
        return await dbContext.Expenses
            .Where(e => e.GroupId == request.GroupId && e.DeletedAt == null)
            .Join(dbContext.Users, e => e.PaidBy, u => u.Id, (e, u) => new { e, u })
            .OrderByDescending(x => x.e.CreatedAt)
            .Select(x => new ExpenseDto(
                x.e.Id,
                x.e.PaidBy,
                x.u.Name,
                x.e.AmountPaise,
                x.e.Description,
                x.e.SplitType,
                x.e.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }
}
