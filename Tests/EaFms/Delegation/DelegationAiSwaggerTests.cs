using System.Text.Json.Nodes;
using Jarvis5.Controllers.EaFms;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Jarvis5.Tests.EaFms.Delegation;

public class DelegationAiSwaggerTests
{
    [Fact]
    public async Task GeneratedContract_ExposesExactlyTheThreeDelegationAiRoutes_WithNoProviderInternals()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(DelegationAiController).Assembly);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            // Two distinct ApprovalDetailDto classes exist (SCIH's Jarvis5.Dtos.Approval and
            // EaFms's Jarvis5.Dtos.EaFms) — same schemaId collision Travel/ApprovalAiSwaggerTests
            // resolve the same way, since this builds the WHOLE assembly's swagger doc.
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

        var aiPaths = paths.Where(p => p.Key.StartsWith("/api/ea/delegations/") && p.Key.Contains("/ai")).Select(p => p.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[]
        {
            "/api/ea/delegations/{delegationId}/ai/delay-risk",
            "/api/ea/delegations/{delegationId}/ai/predict-due-date",
            "/api/ea/delegations/{delegationId}/ai/predict-due-date/apply",
            "/api/ea/delegations/{delegationId}/ai/suggest-owner",
            "/api/ea/delegations/{delegationId}/ai/suggest-owner/apply",
        }, aiPaths);

        var ownerResponseRef = paths["/api/ea/delegations/{delegationId}/ai/suggest-owner"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/DelegationAiOwnerSuggestionResponseDto", ownerResponseRef);
        var dueDateResponseRef = paths["/api/ea/delegations/{delegationId}/ai/predict-due-date"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/DelegationAiDueDatePredictionResponseDto", dueDateResponseRef);
        var riskResponseRef = paths["/api/ea/delegations/{delegationId}/ai/delay-risk"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/DelegationAiDelayRiskResponseDto", riskResponseRef);

        // None of the three preview reads take a request body — they only read existing state.
        Assert.Null(paths["/api/ea/delegations/{delegationId}/ai/suggest-owner"]!["post"]!["requestBody"]);
        Assert.Null(paths["/api/ea/delegations/{delegationId}/ai/predict-due-date"]!["post"]!["requestBody"]);
        Assert.Null(paths["/api/ea/delegations/{delegationId}/ai/delay-risk"]!["post"]!["requestBody"]);

        // The two apply (write) endpoints DO take a request body and return the real, updated Delegation.
        var applyOwnerRequestRef = paths["/api/ea/delegations/{delegationId}/ai/suggest-owner/apply"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/ApplySuggestedOwnerRequestDto", applyOwnerRequestRef);
        var applyOwnerResponseRef = paths["/api/ea/delegations/{delegationId}/ai/suggest-owner/apply"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/DelegationResponseDto", applyOwnerResponseRef);
        var applyDueDateRequestRef = paths["/api/ea/delegations/{delegationId}/ai/predict-due-date/apply"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/ApplyPredictedDueDateRequestDto", applyDueDateRequestRef);
        var applyDueDateResponseRef = paths["/api/ea/delegations/{delegationId}/ai/predict-due-date/apply"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        Assert.Equal("#/components/schemas/DelegationResponseDto", applyDueDateResponseRef);

        // No API key/config/provider internals anywhere in the generated document.
        Assert.DoesNotContain("ApiKey", json);
        Assert.DoesNotContain("AnthropicSettings", json);
        Assert.DoesNotContain("claude-sonnet", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anthropic.com", json, StringComparison.OrdinalIgnoreCase);
    }
}
