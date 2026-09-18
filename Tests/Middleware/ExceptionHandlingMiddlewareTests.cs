using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jarvis5.Tests.Middleware;

/// <summary>
/// The one proven defect this fixes: BadRequestException had no catch clause in
/// ExceptionHandlingMiddleware (the EA-scoped middleware, mounted only under /api/ea via
/// UseWhen in Program.cs) and fell through to the generic 500 handler. These tests
/// exercise the middleware directly — no HTTP server needed — proving each exception type
/// still maps exactly as before, plus the new BadRequestException -> 400 mapping.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int StatusCode, JsonDocument Body)> InvokeAsync(RequestDelegate next)
    {
        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/ea/delegations";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return (context.Response.StatusCode, JsonDocument.Parse(json));
    }

    [Fact]
    public async Task BadRequestException_MapsTo400_PreservingMessage()
    {
        var (status, body) = await InvokeAsync(_ => throw new BadRequestException("Unsupported status 'Bogus'."));

        Assert.Equal(400, status);
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Bad Request", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Unsupported status 'Bogus'.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task NotFoundException_StillMapsTo404_Unchanged()
    {
        var (status, body) = await InvokeAsync(_ => throw new NotFoundException("Delegation 999999 not found."));

        Assert.Equal(404, status);
        Assert.Equal("Not Found", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Delegation 999999 not found.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task BusinessRuleException_StillMapsTo409_Unchanged()
    {
        var (status, body) = await InvokeAsync(_ => throw new BusinessRuleException("Delegation cannot be completed from its current status 'Pending'."));

        Assert.Equal(409, status);
        Assert.Equal("Business Rule Violation", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task InvalidOperationException_StillMapsTo409_Unchanged()
    {
        var (status, body) = await InvokeAsync(_ => throw new InvalidOperationException("Legacy conflict."));

        Assert.Equal(409, status);
        Assert.Equal("Conflict", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UnexpectedException_StillMapsTo500_AndHidesInternalDetail()
    {
        var (status, body) = await InvokeAsync(_ => throw new InvalidCastException("some internal detail that must not leak"));

        Assert.Equal(500, status);
        Assert.Equal("Internal Server Error", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("An unexpected error occurred.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task NoException_PassesThroughUnaffected()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => Task.CompletedTask, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode); // DefaultHttpContext's default, untouched
    }

    // ----------------------------------------------------------------
    // The three concrete Delegation cases the prior audit proved were broken.
    // ----------------------------------------------------------------

    [Fact]
    public async Task DelegationInvalidPriority_MapsTo400_NotServerError()
    {
        // Exact exception DelegationService.ResolvePriorityAsync throws for an unknown name.
        var (status, _) = await InvokeAsync(_ => throw new BadRequestException("Priority 'NotARealPriority' is not an active priority level."));
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task DelegationInvalidStatusFilter_MapsTo400_NotServerError()
    {
        // Exact exception DelegationService.NormalizeStatus throws for an unsupported value.
        var (status, _) = await InvokeAsync(_ => throw new BadRequestException("Unsupported status 'Bogus'."));
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task DelegationInvalidViewFilter_MapsTo400_NotServerError()
    {
        // Exact exception DelegationService.NormalizeView throws for an unsupported value.
        var (status, _) = await InvokeAsync(_ => throw new BadRequestException("Unsupported view 'bogus'."));
        Assert.Equal(400, status);
    }
}
