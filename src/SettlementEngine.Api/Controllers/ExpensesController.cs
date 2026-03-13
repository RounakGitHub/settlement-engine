using MediatR;
using Microsoft.AspNetCore.Mvc;
using SettlementEngine.Application.Commands;

namespace SettlementEngine.Api.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}/expenses")]
public class ExpensesController(ISender mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AddExpense(
        Guid groupId,
        [FromBody] AddExpenseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken ct)
    {
        var command = new AddExpenseCommand(
            GroupId: groupId,
            PaidByUserId: request.PaidByUserId,
            Amount: request.Amount,
            Description: request.Description,
            SplitAmongUserIds: request.SplitAmongUserIds,
            SplitType: request.SplitType,
            IdempotencyKey: idempotencyKey
        );

        var result = await mediator.Send(command, ct);

        return result.WasIdempotent
            ? Ok(result)
            : CreatedAtAction(null, new { id = result.ExpenseEventId }, result);
    }
}

public sealed record AddExpenseRequest(
    Guid PaidByUserId,
    decimal Amount,
    string Description,
    IReadOnlyList<Guid> SplitAmongUserIds,
    string SplitType
);
