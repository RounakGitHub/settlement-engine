using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Handlers.Settlements;
using Splitr.Application.Helpers;
using Splitr.Application.Interfaces;
using Splitr.Application.Queries;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Queries;

public class GetGroupBalancesQueryHandler(IAppDbContext dbContext) : IRequestHandler<GetGroupBalancesQuery, List<UserBalance>>
{
    public async Task<List<UserBalance>> Handle(GetGroupBalancesQuery request, CancellationToken cancellationToken)
    {
        await SettlementExpiryHelper.ExpireStaleSettlements(dbContext, request.GroupId, cancellationToken);

        var members = await dbContext.GroupMembers
            .Where(gm => gm.GroupId == request.GroupId)
            .Join(dbContext.Users, gm => gm.UserId, u => u.Id, (gm, u) => new { u.Id, u.Name })
            .ToListAsync(cancellationToken);

        var expenses = await dbContext.Expenses
            .Where(e => e.GroupId == request.GroupId && e.DeletedAt == null)
            .Include(e => e.Splits)
            .ToListAsync(cancellationToken);

        var settlements = await dbContext.Settlements
            .Where(s => s.GroupId == request.GroupId && s.Status == SettlementStatus.Confirmed)
            .ToListAsync(cancellationToken);

        var balances = BalanceCalculator.Compute(
            members.Select(m => m.Id),
            expenses,
            settlements
        );

        return members
            .Select(m => new UserBalance(m.Id, m.Name, balances.GetValueOrDefault(m.Id)))
            .ToList();
    }
}
