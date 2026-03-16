using Splitr.Application.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Splitr.Application.Commands.Expenses;

namespace Splitr.API.Controllers;

[ApiController]
[Route("api/groups/{groupId:guid}/expenses")]
[Authorize]
[EnableRateLimiting("write")]
public class ExpensesController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AddExpense(Guid groupId, [FromBody] AddExpenseCommand command, CancellationToken ct)
    {
        var expenseId = await sender.Send(command with { GroupId = groupId }, ct);
        return CreatedAtAction(null, new { groupId, id = expenseId }, new { id = expenseId });
    }

    [HttpPut("{expenseId:guid}")]
    public async Task<IActionResult> EditExpense(Guid groupId, Guid expenseId, [FromBody] EditExpenseCommand command, CancellationToken ct)
    {
        await sender.Send(command with { GroupId = groupId, ExpenseId = expenseId }, ct);
        return NoContent();
    }

    [HttpDelete("{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid groupId, Guid expenseId, CancellationToken ct)
    {
        await sender.Send(new DeleteExpenseCommand(groupId, expenseId), ct);
        return NoContent();
    }
}
