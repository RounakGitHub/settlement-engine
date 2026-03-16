using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Splitr.Application.Commands.Settlements;
using Splitr.Application.Exceptions;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Settlements;

public class CancelSettlementCommandHandler(IAppDbContext dbContext, ICurrentUserService currentUser) : IRequestHandler<CancelSettlementCommand, Unit>
{
    public async Task<Unit> Handle(CancelSettlementCommand request, CancellationToken cancellationToken)
    {
        var settlement = await dbContext.Settlements.FirstOrDefaultAsync(
            s => s.Id == request.SettlementId,
            cancellationToken
        ) ?? throw new InvalidOperationException("Settlement not found.");

        if (settlement.Status != SettlementStatus.Pending)
            throw new InvalidOperationException("Only pending settlements can be cancelled.");

        if (settlement.PayerId != currentUser.UserId)
            throw new ForbiddenAccessException("Only the payer can cancel a settlement.");

        settlement.Status = SettlementStatus.Cancelled;

        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.SettlementCancelled, new { settlement.Id, settlement.GroupId }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
