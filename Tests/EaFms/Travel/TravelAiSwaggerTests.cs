using System.Text.Json.Nodes;
using Jarvis5.Controllers.EaFms;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

public class TravelAiSwaggerTests
{
    [Fact]
    public async Task GeneratedContract_ExposesExactlyTheFourTravelAiRoutes_WithNoProviderInternals()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(TravelAiController).Assembly);
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

        var aiPaths = paths.Where(p => p.Key.StartsWith("/api/ea/travel/requests/") && p.Key.Contains("/ai")).Select(p => p.Key).OrderBy(k => k).ToArray();
        Assert.Equal(new[]
        {
            "/api/ea/travel/requests/{travelRequestId}/ai/checklist",
            "/api/ea/travel/requests/{travelRequestId}/ai/itinerary/draft",
            "/api/ea/travel/requests/{travelRequestId}/ai/options/compare",
            "/api/ea/travel/requests/{travelRequestId}/ai/options/confirm",
            "/api/ea/travel/requests/{travelRequestId}/ai/options/suggest",
        }, aiPaths);

        var comparePath = paths["/api/ea/travel/requests/{travelRequestId}/ai/options/compare"]!["post"];
        var compareRequestRef = comparePath!["requestBody"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();
        Assert.Equal("#/components/schemas/TravelAiCompareOptionsRequestDto", compareRequestRef);
        var compareResponseRef = comparePath["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();
        Assert.Equal("#/components/schemas/TravelAiCompareOptionsResponseDto", compareResponseRef);
        var compareRequestSchema = root["components"]!["schemas"]!["TravelAiCompareOptionsRequestDto"]!;
        Assert.Equal("#/components/schemas/CreateTravelBookingDto", compareRequestSchema["properties"]!["options"]!["items"]!["$ref"]!.GetValue<string>());

        // CanCreateBooking is exposed on both pre-booking-decision responses, so the
        // frontend never has to re-derive TravelBookingService's own readiness rule.
        Assert.NotNull(root["components"]!["schemas"]!["TravelAiCompareOptionsResponseDto"]!["properties"]!["canCreateBooking"]);
        Assert.NotNull(root["components"]!["schemas"]!["TravelAiOptionsSuggestionResponseDto"]!["properties"]!["canCreateBooking"]);

        var confirmPath = paths["/api/ea/travel/requests/{travelRequestId}/ai/options/confirm"]!["post"];
        var requestSchemaRef = confirmPath!["requestBody"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();
        Assert.Equal("#/components/schemas/ConfirmTravelAiOptionsRequestDto", requestSchemaRef);
        var responseSchemaRef = confirmPath["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>();
        Assert.Equal("#/components/schemas/TravelAiOptionsConfirmResponseDto", responseSchemaRef);

        // Confirm's request/response reuse the existing Travel booking DTOs — no parallel shape.
        var requestSchema = root["components"]!["schemas"]!["ConfirmTravelAiOptionsRequestDto"]!;
        Assert.Equal("#/components/schemas/CreateTravelBookingDto", requestSchema["properties"]!["options"]!["items"]!["$ref"]!.GetValue<string>());
        var responseSchema = root["components"]!["schemas"]!["TravelAiOptionsConfirmResponseDto"]!;
        Assert.Equal("#/components/schemas/TravelBookingResponseDto", responseSchema["properties"]!["createdBookings"]!["items"]!["$ref"]!.GetValue<string>());

        // No API key/config/provider internals anywhere in the generated document.
        Assert.DoesNotContain("ApiKey", json);
        Assert.DoesNotContain("AnthropicSettings", json);
        Assert.DoesNotContain("claude-sonnet", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("anthropic.com", json, StringComparison.OrdinalIgnoreCase);
    }
}
