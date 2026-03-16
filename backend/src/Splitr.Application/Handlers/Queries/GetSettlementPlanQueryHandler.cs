using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Handlers.Settlements;
using Splitr.Application.Helpers;
using Splitr.Application.Interfaces;
using Splitr.Application.Queries;
using Splitr.Domain.Algorithms;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Queries;

public class GetSettlementPlanQueryHandler(IAppDbContext dbContext) : IRequestHandler<GetSettlementPlanQuery, List<Transfer>>
{
    public async Task<List<Transfer>> Handle(GetSettlementPlanQuery request, CancellationToken cancellationToken)
    {
        await SettlementExpiryHelper.ExpireStaleSettlements(dbContext, request.GroupId, cancellationToken);

        var memberIds = await dbContext.GroupMembers
            .Where(gm => gm.GroupId == request.GroupId)
            .Select(gm => gm.UserId)
            .ToListAsync(cancellationToken);

        var expenses = await dbContext.Expenses
            .Where(e => e.GroupId == request.GroupId && e.DeletedAt == null)
            .Include(e => e.Splits)
            .ToListAsync(cancellationToken);

        var settlements = await dbContext.Settlements
            .Where(s => s.GroupId == request.GroupId && s.Status == SettlementStatus.Confirmed)
            .ToListAsync(cancellationToken);

        var balances = BalanceCalculator.Compute(memberIds, expenses, settlements);

        return DebtSimplifier.Simplify(balances);
    }
}
