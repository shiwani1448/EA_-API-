using System.Text.Json.Nodes;
using Jarvis5.Controllers.EaFms;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Jarvis5.Tests.EaFms.Approvals;

public class ApprovalAiSwaggerTests
{
    [Fact]
    public async Task GeneratedContract_ExposesAllFiveApprovalAiRoutes_WithNoProviderInternals()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(ApprovalAiController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            // Two distinct ApprovalDetailDto classes exist (SCIH's Jarvis5.Dtos.Approval and
            // EaFms's Jarvis5.Dtos.EaFms) — same schemaId collision TravelAiSwaggerTests
            // resolves the same way, since this builds the WHOLE assembly's swagger doc.
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

        var aiPaths = paths.Where(p => p.Key.StartsWith("/api/ea/approvals/") && p.Key.Contains("/ai")).Select(p => p.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[]
        {
            "/api/ea/approvals/{approvalRequestId}/ai/readiness",
            "/api/ea/approvals/{approvalRequestId}/ai/recommend-approver",
            "/api/ea/approvals/{approvalRequestId}/ai/recommend-approver/apply",
            "/api/ea/approvals/{approvalRequestId}/ai/status-summary",
            "/api/ea/approvals/ai/readiness",
            "/api/ea/approvals/ai/recommend-approver",
        }.OrderBy(k => k).ToArray(), aiPaths);

        var readinessResponseRef = paths["/api/ea/approvals/{approvalRequestId}/ai/readiness"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/ApprovalAiReadinessResponseDto", readinessResponseRef);
        var approverResponseRef = paths["/api/ea/approvals/{approvalRequestId}/ai/recommend-approver"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/ApprovalAiApproverSuggestionResponseDto", approverResponseRef);
        var statusResponseRef = paths["/api/ea/approvals/{approvalRequestId}/ai/status-summary"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/ApprovalAiStatusSummaryResponseDto", statusResponseRef);

        // None of the three preview reads take a request body — they only read existing state.
        Assert.Null(paths["/api/ea/approvals/{approvalRequestId}/ai/readiness"]!["post"]!["requestBody"]);
        Assert.Null(paths["/api/ea/approvals/{approvalRequestId}/ai/recommend-approver"]!["post"]!["requestBody"]);
        Assert.Null(paths["/api/ea/approvals/{approvalRequestId}/ai/status-summary"]!["post"]!["requestBody"]);

        // The apply (write) endpoint DOES take a request body and returns the real, updated ApprovalDetailDto.
        var applyRequestRef = paths["/api/ea/approvals/{approvalRequestId}/ai/recommend-approver/apply"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/ApplyApproverSuggestionRequestDto", applyRequestRef);
        var applyResponseRef = paths["/api/ea/approvals/{approvalRequestId}/ai/recommend-approver/apply"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/EaApprovalDetailDto", applyResponseRef);

        // No API key/config/provider internals anywhere in the generated document.
        Assert.DoesNotContain("ApiKey", json);
        Assert.DoesNotContain("AnthropicSettings", json);
        Assert.DoesNotContain("claude-sonnet", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anthropic.com", json, StringComparison.OrdinalIgnoreCase);
    }
}
