using Splitr.Application.Mediator;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Settlements;
using Splitr.Application.Configuration;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Settlements;

public class InitiateSettlementCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IOptions<SettlementOptions> settlementOptions) : IRequestHandler<InitiateSettlementCommand, InitiateSettlementResult>
{
    private readonly SettlementOptions _options = settlementOptions.Value;

    public async Task<InitiateSettlementResult> Handle(InitiateSettlementCommand request, CancellationToken cancellationToken)
    {
        await SettlementExpiryHelper.ExpireStaleSettlements(dbContext, request.GroupId, cancellationToken);

        var settlement = new Settlement
        {
            GroupId = request.GroupId,
            PayerId = currentUser.UserId,
            PayeeId = request.PayeeId,
            AmountPaise = request.AmountPaise,
            Status = SettlementStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddHours(_options.ExpiryHours),
            RazorpayOrderId = $"order_{Guid.NewGuid():N}" // placeholder until Razorpay integration
        };

        dbContext.Settlements.Add(settlement);
        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.SettlementProposed, new { settlement.Id, settlement.GroupId, settlement.PayerId, settlement.PayeeId, settlement.AmountPaise }));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new InitiateSettlementResult(settlement.Id, settlement.RazorpayOrderId!);
    }
}