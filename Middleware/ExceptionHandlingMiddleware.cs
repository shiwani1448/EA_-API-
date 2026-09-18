using Jarvis5.Common;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "Not Found", ex.Message);
        }
        catch (BusinessRuleException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Business Rule Violation", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Preserve existing project convention where InvalidOperationException is treated
            // as a conflict (409). This allows older modules that throw InvalidOperationException
            // to keep returning 409 when this middleware is registered before other handlers.
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (BadRequestException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Bad Request", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}
