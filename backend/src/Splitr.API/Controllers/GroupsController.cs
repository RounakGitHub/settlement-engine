using Splitr.Application.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Splitr.Application.Commands.Groups;
using Splitr.Application.Queries;

namespace Splitr.API.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public class GroupsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMyGroups(CancellationToken ct)
    {
        var groups = await sender.Send(new GetUserGroupsQuery(), ct);
        return Ok(groups);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return CreatedAtAction(nameof(GetBalances), new { id = result.GroupId }, result);
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken ct)
    {
        var members = await sender.Send(new GetGroupMembersQuery(id), ct);
        return Ok(members);
    }

    [HttpGet("join/{code}")]
    [AllowAnonymous]
    public async Task<IActionResult> Preview(string code, CancellationToken ct)
    {
        var preview = await sender.Send(new GetGroupPreviewQuery(code), ct);
        return Ok(preview);
    }

    [HttpPost("join/{code}")]
    public async Task<IActionResult> Join(string code, CancellationToken ct)
    {
        await sender.Send(new JoinGroupCommand(code), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await sender.Send(new LeaveGroupCommand(id), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteGroupCommand(id), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/regenerate-invite")]
    public async Task<IActionResult> RegenerateInvite(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RegenerateInviteCodeCommand(id), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/balances")]
    public async Task<IActionResult> GetBalances(Guid id, CancellationToken ct)
    {
        var balances = await sender.Send(new GetGroupBalancesQuery(id), ct);
        return Ok(balances);
    }

    [HttpGet("{id:guid}/expenses")]
    public async Task<IActionResult> GetExpenses(Guid id, CancellationToken ct)
    {
        var expenses = await sender.Send(new GetGroupExpensesQuery(id), ct);
        return Ok(expenses);
    }

    [HttpGet("{id:guid}/settlement-plan")]
    public async Task<IActionResult> GetSettlementPlan(Guid id, CancellationToken ct)
    {
        var plan = await sender.Send(new GetSettlementPlanQuery(id), ct);
        return Ok(plan);
    }
}
