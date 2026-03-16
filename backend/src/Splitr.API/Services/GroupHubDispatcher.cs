using Microsoft.AspNetCore.SignalR;
using Splitr.API.Hubs;
using Splitr.Application.Interfaces;

namespace Splitr.API.Services;

public class GroupHubDispatcher(IHubContext<GroupHub, IGroupHubClient> hubContext) : ISignalRDispatcher
{
    public async Task DispatchAsync(Guid groupId, string eventType, string payload, CancellationToken ct)
    {
        var group = hubContext.Clients.Group($"group:{groupId}");

        await (eventType switch
        {
            "ExpenseAdded" => group.ExpenseAdded(payload),
            "ExpenseEdited" => group.ExpenseEdited(payload),
            "ExpenseDeleted" => group.ExpenseDeleted(payload),
            "SettlementConfirmed" => group.SettlementConfirmed(payload),
            "SettlementProposed" => group.SettlementProposed(payload),
            "MemberJoined" => group.MemberJoined(payload),
            "MemberLeft" => group.MemberLeft(payload),
            "DebtGraphUpdated" => group.DebtGraphUpdated(payload),
            _ => Task.CompletedTask
        });
    }
}
