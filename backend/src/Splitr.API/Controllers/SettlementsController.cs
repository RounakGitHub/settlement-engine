using Splitr.Application.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Splitr.Application.Commands.Settlements;

namespace Splitr.API.Controllers;

[ApiController]
[Route("api/settlements")]
[Authorize]
public class SettlementsController(ISender sender) : ControllerBase
{
    [HttpPost("initiate")]
    [EnableRateLimiting("settlement")]
    public async Task<IActionResult> Initiate([FromBody] InitiateSettlementCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await sender.Send(new CancelSettlementCommand(id), ct);
        return NoContent();
    }

    [HttpPost("~/api/webhooks/razorpay")]
    [AllowAnonymous]
    public async Task<IActionResult> RazorpayWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync();
        var signature = Request.Headers["X-Razorpay-Signature"].ToString();
        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        await sender.Send(new ProcessWebhookCommand(rawBody, signature, sourceIp));
        return Ok();
    }
}
