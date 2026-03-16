using Microsoft.Extensions.Options;
using Splitr.Application.Configuration;
using Splitr.Application.Interfaces;

namespace Splitr.API.Middleware;

public class IdempotencyMiddleware(RequestDelegate next)
{
    private const string IdempotencyKeyHeader = "X-Idempotency-Key";

    public async Task InvokeAsync(HttpContext context, IIdempotencyService idempotencyService, IOptions<SettlementOptions> settlementOptions)
    {
        if (context.Request.Method != HttpMethods.Post || !context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var idempotencyKey) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await next(context);
            return;
        }

        var key = idempotencyKey.ToString();

        var cachedResponse = await idempotencyService.GetAsync(key, context.RequestAborted);
        if (cachedResponse is not null)
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cachedResponse, context.RequestAborted);
            return;
        }

        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await next(context);

        if (context.Response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status201Created)
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            var body = await new StreamReader(responseBody).ReadToEndAsync(context.RequestAborted);

            await idempotencyService.SetAsync(key, body, TimeSpan.FromHours(settlementOptions.Value.IdempotencyTtlHours), context.RequestAborted);
        }

        responseBody.Seek(0, SeekOrigin.Begin);
        await responseBody.CopyToAsync(originalBodyStream, context.RequestAborted);

        context.Response.Body = originalBodyStream;
    }
}