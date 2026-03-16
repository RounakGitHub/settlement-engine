using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Splitr.API.Hubs;

public interface IGroupHubClient
{
    Task ExpenseAdded(object expense);
    Task ExpenseEdited(object expense);
    Task ExpenseDeleted(object expense);
    Task SettlementConfirmed(object settlement);
    Task SettlementProposed(object settlement);
    Task SettlementFailed(object settlement);
    Task BalanceUpdated(object balances);
    Task MemberJoined(object member);
    Task MemberLeft(object member);
    Task DebtGraphUpdated(object plan);
}

[Authorize]
public class GroupHub : Hub<IGroupHubClient>
{
    public async Task JoinGroup(string groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"group:{groupId}");
    }

    public async Task LeaveGroup(string groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group:{groupId}");
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        await base.OnDisconnectedAsync(exception);
    }
}
