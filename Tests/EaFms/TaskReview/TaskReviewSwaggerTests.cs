using System.Text.Json.Nodes;
using Jarvis5.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Jarvis5.Tests.EaFms.TaskReview;

public class TaskReviewSwaggerTests
{
    [Fact]
    public async Task GeneratedContract_ExposesModuleRoutes_AndReusesSummary()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(MeetingsLifecycleController).Assembly);
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
        foreach (var prefix in new[] { "/api/ea/delegations/", "/api/ea/approvals/" })
        {
            foreach (var suffix in new[] { "/submit-for-review", "/review/approve", "/review/rework", "/review/history" })
            {
                var path = Assert.Single(paths.Where(p => p.Key.StartsWith(prefix) && p.Key.EndsWith(suffix)));
                var verb = suffix.EndsWith("history") ? "get" : "post";
                Assert.NotNull(path.Value![verb]?["responses"]?["200"]?["content"]?["application/json"]?["schema"]);
            }
        }
        var reviewPaths = paths.Where(p => p.Key.EndsWith("/submit-for-review") || p.Key.EndsWith("/review/approve") || p.Key.EndsWith("/review/rework") || p.Key.EndsWith("/review/history")).ToList();
        Assert.Equal(8, reviewPaths.Count);
        Assert.DoesNotContain(reviewPaths, p => p.Key.StartsWith("/api/ea/meetings/") || p.Key.StartsWith("/api/ea/travel/"));
        Assert.DoesNotContain(paths, p => p.Key.StartsWith("/api/ea/task-reviews"));
        var schemas = root["components"]!["schemas"]!;
        foreach (var name in new[] { "DelegationResponseDto", "ApprovalListItemDto", "EaApprovalDetailDto" })
            Assert.True(schemas[name]?["properties"]?["reviewSummary"]?["$ref"]?.GetValue<string>() == "#/components/schemas/TaskReviewSummaryDto", name);
        foreach (var name in new[] { "MeetingDetailResponseDto", "MeetingListItemResponseDto", "MeetingLifecycleResponseDto", "MeetingPauseResponseDto", "TravelRequestDetailDto", "TravelRequestListItemDto", "TravelRequestCreatedDto", "TravelActionResponseDto" })
        {
            Assert.NotNull(schemas[name]);
            Assert.Null(schemas[name]?["properties"]?["reviewSummary"]);
        }
        Assert.Null(schemas["TaskReviewHistoryItemDto"]?["properties"]?["workflowInstanceId"]);
        foreach (var name in new[] { "SubmitForReviewRequestDto", "ApproveTaskReviewRequestDto", "RequestTaskReworkRequestDto" })
            Assert.True(schemas[name]?["required"] is null || schemas[name]!["required"]!.AsArray().Count == 0);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "task-review-swagger.json"), json);
    }
}
