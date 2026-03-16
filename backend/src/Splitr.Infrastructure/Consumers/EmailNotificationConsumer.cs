using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Splitr.Application.Interfaces;
using Splitr.Domain.Enums;
using Splitr.Infrastructure.Configuration;
using Splitr.Infrastructure.Messaging;
using Splitr.Infrastructure.Persistence;

namespace Splitr.Infrastructure.Consumers;

public class EmailNotificationConsumer(
    IOptions<KafkaOptions> kafkaOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<EmailNotificationConsumer> logger) : KafkaConsumerService(kafkaOptions, logger)
{
    private static readonly string[] NotifiableEvents =
    [
        nameof(EventType.ExpenseAdded),
        nameof(EventType.ExpenseEdited),
        nameof(EventType.ExpenseDeleted),
        nameof(EventType.SettlementConfirmed),
        nameof(EventType.SettlementFailed),
        nameof(EventType.SettlementExpired),
        nameof(EventType.MemberJoined),
        nameof(EventType.MemberLeft)
    ];

    protected override IReadOnlyList<string> Topics => Kafka.GetTopicsForEvents(NotifiableEvents);
    protected override string ConsumerGroupId => Kafka.Consumers.EmailNotificationGroupId;

    protected override async Task ProcessMessageAsync(string key, string value, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(value);
        var root = doc.RootElement;

        if (!root.TryGetProperty("EventType", out var eventTypeProp) ||
            !root.TryGetProperty("Data", out var data))
            return;

        var eventType = eventTypeProp.GetString()!;

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        switch (eventType)
        {
            case nameof(EventType.ExpenseAdded):
                await HandleExpenseAdded(dbContext, emailService, data, ct);
                break;
            case nameof(EventType.ExpenseEdited):
                await HandleExpenseModified(dbContext, emailService, data, "updated", ct);
                break;
            case nameof(EventType.ExpenseDeleted):
                await HandleExpenseModified(dbContext, emailService, data, "removed", ct);
                break;
            case nameof(EventType.SettlementConfirmed):
                await HandleSettlementConfirmed(dbContext, emailService, data, ct);
                break;
            case nameof(EventType.SettlementFailed):
                await HandleSettlementFailed(dbContext, emailService, data, ct);
                break;
            case nameof(EventType.SettlementExpired):
                await HandleSettlementExpired(dbContext, emailService, data, ct);
                break;
            case nameof(EventType.MemberJoined):
                await HandleMemberChange(dbContext, emailService, data, "joined", ct);
                break;
            case nameof(EventType.MemberLeft):
                await HandleMemberChange(dbContext, emailService, data, "left", ct);
                break;
        }
    }

    private async Task HandleExpenseAdded(AppDbContext db, IEmailService email, JsonElement data, CancellationToken ct)
    {
        var groupId = data.GetProperty("GroupId").GetGuid();
        var paidBy = data.GetProperty("PaidBy").GetGuid();
        var amountPaise = data.GetProperty("AmountPaise").GetInt64();

        var group = await db.Groups.FindAsync([groupId], ct);
        var payer = await db.Users.FindAsync([paidBy], ct);
        if (group is null || payer is null) return;

        var recipients = await GetGroupMembersExcept(db, groupId, paidBy, ct);
        var amount = FormatAmount(amountPaise, group.Currency);
        var subject = $"{payer.Name} added an expense of {amount} in {group.Name}";
        var body = $"""
            <p><strong>{payer.Name}</strong> added an expense of <strong>{amount}</strong> in <strong>{group.Name}</strong>.</p>
            <p>Open Splitr to see your updated balances.</p>
            """;

        await SendToAll(email, recipients, subject, body, ct);
    }

    private async Task HandleExpenseModified(AppDbContext db, IEmailService email, JsonElement data, string action, CancellationToken ct)
    {
        var groupId = data.GetProperty("GroupId").GetGuid();

        var group = await db.Groups.FindAsync([groupId], ct);
        if (group is null) return;

        var recipients = await GetGroupMembers(db, groupId, ct);
        var subject = $"An expense was {action} in {group.Name}";
        var body = $"""
            <p>An expense was <strong>{action}</strong> in <strong>{group.Name}</strong>.</p>
            <p>Open Splitr to see your updated balances.</p>
            """;

        await SendToAll(email, recipients, subject, body, ct);
    }

    private async Task HandleSettlementConfirmed(AppDbContext db, IEmailService email, JsonElement data, CancellationToken ct)
    {
        var groupId = data.GetProperty("GroupId").GetGuid();
        var settlementId = data.GetProperty("Id").GetGuid();

        var settlement = await db.Settlements.FindAsync([settlementId], ct);
        var group = await db.Groups.FindAsync([groupId], ct);
        if (settlement is null || group is null) return;

        var payer = await db.Users.FindAsync([settlement.PayerId], ct);
        var payee = await db.Users.FindAsync([settlement.PayeeId], ct);
        if (payer is null || payee is null) return;

        var amount = FormatAmount(settlement.AmountPaise, group.Currency);
        var subject = $"Settlement of {amount} confirmed in {group.Name}";
        var body = $"""
            <p><strong>{payer.Name}</strong> paid <strong>{amount}</strong> to <strong>{payee.Name}</strong> in <strong>{group.Name}</strong>.</p>
            <p>The settlement has been confirmed.</p>
            """;

        var recipients = new[] { payer.Email, payee.Email }.Where(e => !string.IsNullOrEmpty(e));
        await SendToAll(email, recipients, subject, body, ct);
    }

    private async Task HandleSettlementFailed(AppDbContext db, IEmailService email, JsonElement data, CancellationToken ct)
    {
        var groupId = data.GetProperty("GroupId").GetGuid();
        var settlementId = data.GetProperty("Id").GetGuid();

        var settlement = await db.Settlements.FindAsync([settlementId], ct);
        var group = await db.Groups.FindAsync([groupId], ct);
        if (settlement is null || group is null) return;

        var payer = await db.Users.FindAsync([settlement.PayerId], ct);
        if (payer is null || string.IsNullOrEmpty(payer.Email)) return;

        var amount = FormatAmount(settlement.AmountPaise, group.Currency);
        var subject = $"Your settlement of {amount} in {group.Name} has failed";
        var body = $"""
            <p>Your payment of <strong>{amount}</strong> in <strong>{group.Name}</strong> could not be processed.</p>
            <p>Open Splitr to try again.</p>
            """;

        await SendSafe(email, payer.Email, subject, body, ct);
    }

    private async Task HandleSettlementExpired(AppDbContext db, IEmailService email, JsonElement data, CancellationToken ct)
    {
        var groupId = data.GetProperty("GroupId").GetGuid();
        var settlementId = data.GetProperty("Id").GetGuid();

        var settlement = await db.Settlements.FindAsync([settlementId], ct);
        var group = await db.Groups.FindAsync([groupId], ct);
        if (settlement is null || group is null) return;

        var payer = await db.Users.FindAsync([settlement.PayerId], ct);
        if (payer is null || string.IsNullOrEmpty(payer.Email)) return;

        var amount = FormatAmount(settlement.AmountPaise, group.Currency);
        var subject = $"Your settlement of {amount} in {group.Name} has expired";
        var body = $"""
            <p>Your pending settlement of <strong>{amount}</strong> in <strong>{group.Name}</strong> has expired.</p>
            <p>Open Splitr to initiate a new settlement if needed.</p>
            """;

        await SendSafe(email, payer.Email, subject, body, ct);
    }

    private async Task HandleMemberChange(AppDbContext db, IEmailService email, JsonElement data, string action, CancellationToken ct)
    {
        var groupId = data.GetProperty("GroupId").GetGuid();
        var userId = data.GetProperty("UserId").GetGuid();

        var group = await db.Groups.FindAsync([groupId], ct);
        var user = await db.Users.FindAsync([userId], ct);
        if (group is null || user is null) return;

        var recipients = await GetGroupMembersExcept(db, groupId, userId, ct);
        var subject = $"{user.Name} {action} {group.Name}";
        var body = $"""
            <p><strong>{user.Name}</strong> has {action} <strong>{group.Name}</strong>.</p>
            """;

        await SendToAll(email, recipients, subject, body, ct);
    }

    private async Task SendToAll(IEmailService email, IEnumerable<string> recipients, string subject, string body, CancellationToken ct)
    {
        foreach (var recipient in recipients)
        {
            await SendSafe(email, recipient, subject, body, ct);
        }
    }

    private async Task SendSafe(IEmailService email, string to, string subject, string body, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(to, subject, body, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipient}", to);
        }
    }

    private static async Task<List<string>> GetGroupMembers(AppDbContext db, Guid groupId, CancellationToken ct)
    {
        return await db.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Join(db.Users, gm => gm.UserId, u => u.Id, (_, u) => u.Email)
            .Where(e => e != "")
            .ToListAsync(ct);
    }

    private static async Task<List<string>> GetGroupMembersExcept(AppDbContext db, Guid groupId, Guid excludeUserId, CancellationToken ct)
    {
        return await db.GroupMembers
            .Where(gm => gm.GroupId == groupId && gm.UserId != excludeUserId)
            .Join(db.Users, gm => gm.UserId, u => u.Id, (_, u) => u.Email)
            .Where(e => e != "")
            .ToListAsync(ct);
    }

    private static string FormatAmount(long amountPaise, string currency) =>
        $"{currency} {amountPaise / 100m:N2}";
}
