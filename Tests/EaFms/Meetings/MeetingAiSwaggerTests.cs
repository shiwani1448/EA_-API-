using System.Text.Json.Nodes;
using Jarvis5.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

public class MeetingAiSwaggerTests
{
    [Fact]
    public async Task GeneratedContract_ExposesTheAnalyzeRoute_WithNoProviderInternals()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(MeetingsAiController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            var defaultSchemaId = new Swashbuckle.AspNetCore.SwaggerGen.SchemaGeneratorOptions().SchemaIdSelector;
            options.CustomSchemaIds(type => type == typeof(Jarvis5.Dtos.EaFms.ApprovalDetailDto) ? "EaApprovalDetailDto" : defaultSchemaId(type));
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "EA contract test", Version = "v1" });
            options.SchemaFilter<Jarvis5.Filters.EaCreateRequestSchemaFilter>();
            options.OperationFilter<Jarvis5.Filters.WorkflowOperationFilter>();
            options.SchemaFilter<Jarvis5.Filters.WorkflowRequestSchemaFilter>();
        });
        await using var app = builder.Build(); // No server or hosted jobs are started.
        app.MapControllers();
        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        var root = JsonNode.Parse(json)!;
        var paths = root["paths"]!.AsObject();

        var path = Assert.Single(paths.Where(p => p.Key == "/api/ea/meetings/{meetingId}/ai/analyze"));
        var post = path.Value!["post"];
        Assert.NotNull(post);
        var responseSchemaRef = post!["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();
        Assert.Equal("#/components/schemas/MeetingAiAnalysisResponseDto", responseSchemaRef);

        var schema = root["components"]!["schemas"]!["MeetingAiAnalysisResponseDto"]!;
        var properties = schema["properties"]!.AsObject().Select(p => p.Key).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "meetingId", "momUsed", "pdfUsed", "proposedActions", "warningMessage" }, properties);

        var actionSchema = root["components"]!["schemas"]!["MeetingAiProposedActionDto"]!;
        var actionProperties = actionSchema["properties"]!.AsObject().Select(p => p.Key).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "description", "doerName", "dueDate", "priority", "title" }, actionProperties);

        // No API key/config/provider internals anywhere in the generated document.
        Assert.DoesNotContain("ApiKey", json);
        Assert.DoesNotContain("AnthropicSettings", json);
        Assert.DoesNotContain("claude-sonnet", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anthropic.com", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GeneratedContract_ExposesExactlyAnalyzeAndConfirm_NoExtraAiRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(MeetingsAiController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            var defaultSchemaId = new Swashbuckle.AspNetCore.SwaggerGen.SchemaGeneratorOptions().SchemaIdSelector;
            options.CustomSchemaIds(type => type == typeof(Jarvis5.Dtos.EaFms.ApprovalDetailDto) ? "EaApprovalDetailDto" : defaultSchemaId(type));
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "EA contract test", Version = "v1" });
            options.SchemaFilter<Jarvis5.Filters.EaCreateRequestSchemaFilter>();
            options.OperationFilter<Jarvis5.Filters.WorkflowOperationFilter>();
            options.SchemaFilter<Jarvis5.Filters.WorkflowRequestSchemaFilter>();
        });
        await using var app = builder.Build();
        app.MapControllers();
        var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        var root = JsonNode.Parse(json)!;
        var paths = root["paths"]!.AsObject();

        var aiPaths = paths.Where(p => p.Key.StartsWith("/api/ea/meetings/") && p.Key.Contains("/ai")).Select(p => p.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[]
        {
            "/api/ea/meetings/{meetingId}/ai/actions/confirm",
            "/api/ea/meetings/{meetingId}/ai/analyze",
        }, aiPaths);
        // No delete/retry/regenerate/history APIs in this phase.
        Assert.DoesNotContain(aiPaths, k => k.Contains("retry") || k.Contains("regenerate") || k.Contains("history"));

        var confirmPath = paths["/api/ea/meetings/{meetingId}/ai/actions/confirm"]!["post"];
        Assert.NotNull(confirmPath);
        var requestSchemaRef = confirmPath!["requestBody"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();
        Assert.Equal("#/components/schemas/ConfirmMeetingAiActionsRequestDto", requestSchemaRef);
        var responseSchemaRef = confirmPath["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();
        Assert.Equal("#/components/schemas/MeetingAiActionsConfirmResponseDto", responseSchemaRef);

        // The confirm request/response reuse the existing MeetingAction DTOs verbatim —
        // no parallel AI-specific action shape.
        var requestSchema = root["components"]!["schemas"]!["ConfirmMeetingAiActionsRequestDto"]!;
        var actionsRef = requestSchema["properties"]!["actions"]!["items"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/CreateMeetingActionDto", actionsRef);

        var responseSchema = root["components"]!["schemas"]!["MeetingAiActionsConfirmResponseDto"]!;
        var createdActionsRef = responseSchema["properties"]!["createdActions"]!["items"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/MeetingActionDto", createdActionsRef);
    }
}
