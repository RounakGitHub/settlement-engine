using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Splitr.Application.Handlers.Settlements;
using Splitr.Domain.Algorithms;
using Splitr.Domain.Entities;
using Splitr.Domain.Enums;
using Splitr.Infrastructure.Configuration;
using Splitr.Infrastructure.Messaging;
using Splitr.Infrastructure.Persistence;

namespace Splitr.Infrastructure.Consumers;

public class DebtSimplifierConsumer(IOptions<KafkaOptions> kafkaOptions, IServiceScopeFactory scopeFactory, ILogger<DebtSimplifierConsumer> logger) : KafkaConsumerService(kafkaOptions, logger)
{
    private static readonly string[] ExpenseEventTypes =
    [
        nameof(EventType.ExpenseAdded),
        nameof(EventType.ExpenseEdited),
        nameof(EventType.ExpenseDeleted)
    ];

    protected override IReadOnlyList<string> Topics => Kafka.GetTopicsForEvents(ExpenseEventTypes);
    protected override string ConsumerGroupId => Kafka.Consumers.DebtSimplifierGroupId;

    protected override async Task ProcessMessageAsync(string key, string value, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(value);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Data", out var data) || !data.TryGetProperty("GroupId", out var groupIdProp))
            return;

        var groupId = groupIdProp.GetGuid();

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await SettlementExpiryHelper.ExpireStaleSettlements(dbContext, groupId, ct);

        var splits = await (
            from e in dbContext.Expenses
            where e.GroupId == groupId && e.DeletedAt == null
            from s in e.Splits
            select new { e.PaidBy, s.UserId, s.AmountPaise }
        ).ToListAsync(ct);

        var netBalances = new Dictionary<Guid, long>();

        foreach (var split in splits)
        {
            netBalances.TryAdd(split.PaidBy, 0);
            netBalances.TryAdd(split.UserId, 0);
            netBalances[split.PaidBy] += split.AmountPaise;
            netBalances[split.UserId] -= split.AmountPaise;
        }

        var transfers = DebtSimplifier.Simplify(netBalances);

        var payload = new { GroupId = groupId, Transfers = transfers };
        dbContext.OutboxEvents.Add(OutboxEvent.From(EventType.DebtGraphUpdated, payload));
        await dbContext.SaveChangesAsync(ct);

        logger.LogDebug("Computed debt graph for group {GroupId}: {TransferCount} transfers", groupId, transfers.Count);
    }
}
