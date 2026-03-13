using MediatR;
using Microsoft.AspNetCore.Mvc;
using SettlementEngine.Application.Commands;

namespace SettlementEngine.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}/settlements")]
public class SettlementsController(ISender mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Propose(
        Guid groupId,
        [FromBody] ProposeSettlementRequest request,
        CancellationToken ct)
    {
        var command = new ProposeSettlementCommand(
            GroupId: groupId,
            FromUserId: request.FromUserId,
            ToUserId: request.ToUserId,
            Amount: request.Amount,
            Note: request.Note
        );

        var result = await mediator.Send(command, ct);
        return CreatedAtAction(null, new { id = result.SettlementId }, result);
    }

    [HttpPost("{settlementId:guid}/confirm")]
    public async Task<IActionResult> Confirm(
        Guid groupId,
        Guid settlementId,
        [FromBody] ConfirmSettlementRequest request,
        CancellationToken ct)
    {
        var command = new ConfirmSettlementCommand(
            GroupId: groupId,
            SettlementId: settlementId,
            ConfirmedByUserId: request.ConfirmedByUserId
        );

        var result = await mediator.Send(command, ct);

        return result.Success
            ? Ok(result)
            : Conflict(new { error = result.Error });
    }
}

public sealed record ProposeSettlementRequest(
    Guid FromUserId,
    Guid ToUserId,
    decimal Amount,
    string? Note
);

public sealed record ConfirmSettlementRequest(Guid ConfirmedByUserId);
