using MediatR;
using Microsoft.AspNetCore.Mvc;
using SettlementEngine.Application.Queries;

namespace SettlementEngine.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}/balances")]
public class BalancesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBalances(Guid groupId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetGroupBalancesQuery(groupId), ct);
        return Ok(result);
    }
}
