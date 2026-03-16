using System.Text.Json;
using Splitr.Application.Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Splitr.Application.Commands.Settlements;
using Splitr.Application.Configuration;
using Splitr.Application.Interfaces;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;

namespace Splitr.Application.Handlers.Settlements;

public class ProcessWebhookCommandHandler(
    IAppDbContext dbContext,
    IWebhookVerifier webhookVerifier,
    IDistributedLockService lockService,
    IOptions<SettlementOptions> settlementOptions) : IRequestHandler<ProcessWebhookCommand, Unit>
{
    private readonly SettlementOptions _options = settlementOptions.Value;

    public async Task<Unit> Handle(ProcessWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!webhookVerifier.VerifySignature(request.RawBody, request.Signature))
            throw new UnauthorizedAccessException("Invalid webhook signature.");

        if (!webhookVerifier.IsAllowedIp(request.SourceIp))
            throw new UnauthorizedAccessException("Webhook source IP not allowed.");

        using var doc = JsonDocument.Parse(request.RawBody);
        var root = doc.RootElement;

        var eventType = root.GetProperty("event").GetString();
        var payload = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");
        var razorpayOrderId = payload.GetProperty("order_id").GetString()!;
        var amountPaise = payload.GetProperty("amount").GetInt64();
        var razorpayPaymentId = payload.GetProperty("id").GetString()!;

        var settlement = await dbContext.Settlements.FirstOrDefaultAsync(
            s => s.RazorpayOrderId == razorpayOrderId,
            cancellationToken
        ) ?? throw new InvalidOperationException("Settlement not found for this order.");

        // Already processed — idempotent
        if (settlement.Status != SettlementStatus.Pending)
            return Unit.Value;

        var lockKey = $"settlement:{settlement.Id}";
        var acquired = await lockService.AcquireAsync(lockKey, TimeSpan.FromSeconds(_options.LockTimeoutSeconds), cancellationToken);
        if (!acquired)
            throw new InvalidOperationException("Could not acquire lock. Retry later.");

        try
        {
            if (eventType == "payment.captured")
            {
                if (amountPaise != settlement.AmountPaise)
                {
                    settlement.Status = SettlementStatus.Review;
                    settlement.RazorpayPaymentId = razorpayPaymentId;
                }
                else
                {
                    settlement.Status = SettlementStatus.Confirmed;
                    settlement.ConfirmedAt = DateTime.UtcNow;
                    settlement.RazorpayPaymentId = razorpayPaymentId;
                    dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.SettlementConfirmed, new { settlement.Id, settlement.GroupId }));
                }
            }
            else if (eventType == "payment.failed")
            {
                settlement.Status = SettlementStatus.Failed;
                settlement.RazorpayPaymentId = razorpayPaymentId;
                dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.SettlementFailed, new { settlement.Id, settlement.GroupId }));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await lockService.ReleaseAsync(lockKey, cancellationToken);
        }

        return Unit.Value;
    }
}