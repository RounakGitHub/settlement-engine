using System.Text.Json;
using FluentValidation;
using Splitr.Application.Exceptions;

namespace Splitr.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());

            await WriteResponse(context, 400,
                "One or more validation errors occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                errors);
        }
        catch (UnauthorizedAccessException ex)
        {
            // 401: Authentication failure — caller is not identified
            await WriteResponse(context, 401,
                ex.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.2");
        }
        catch (ForbiddenAccessException ex)
        {
            // 403: Authorization failure — caller is identified but lacks permission
            await WriteResponse(context, 403,
                ex.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.4");
        }
        catch (InvalidOperationException ex)
        {
            await WriteResponse(context, 400,
                ex.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");

            await WriteResponse(context, 500,
                "An unexpected error occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1");
        }
    }

    private static async Task WriteResponse(
        HttpContext context, int statusCode, string title, string type,
        Dictionary<string, string[]>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        object problem = errors is not null
            ? new { type, title, status = statusCode, errors }
            : new { type, title, status = statusCode };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
