using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Jarvis5.Common;
using Jarvis5.Controllers.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Middleware;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Approvals;

public class ApprovalAiHttpTests
{
    [Fact]
    public async Task PreviewRoutes_BindOptionalBodies_RejectMalformedFields_AndShareErrorHandling()
    {
        var service = new Mock<IApprovalAiService>();
        service.Setup(s => s.CheckReadinessAsync(It.IsAny<ApprovalAiReadinessInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalAiReadinessResponseDto());
        service.Setup(s => s.RecommendApproverAsync(It.IsAny<ApprovalAiApproverInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApprovalAiApproverSuggestionResponseDto());
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(service.Object);
        // The controller is [Authorize]; this host tests binding/error handling, not auth, so allow all.
        builder.Services.AddAuthorization(o => o.DefaultPolicy =
            new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());
        builder.Services.AddControllers().AddApplicationPart(typeof(ApprovalAiController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new DateTimeUtcConverter());
                options.JsonSerializerOptions.Converters.Add(new NullableDateTimeUtcConverter());
            });
        await using var app = builder.Build();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.MapControllers();
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        foreach (var route in new[] { "readiness", "recommend-approver" })
        {
            foreach (var body in new[] { "", "{}", "{\"department\":\"Ops\"}" })
            {
                var response = await client.PostAsync($"/api/ea/approvals/ai/{route}", Json(body));
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var result = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
                Assert.True(result.ContainsKey("approvalRequestId"));
                Assert.Null(result["approvalRequestId"]);
            }
            foreach (var body in new[] { "{\"amount\":\"not a number\"}", "{" })
            {
                var response = await client.PostAsync($"/api/ea/approvals/ai/{route}", Json(body));
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }
        var invalidDate = await client.PostAsync("/api/ea/approvals/ai/readiness", Json("{\"requiredApprovalDate\":\"invalid\"}"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidDate.StatusCode);
        var validDate = await client.PostAsync("/api/ea/approvals/ai/readiness", Json("{\"requiredApprovalDate\":\"2026-09-30\",\"documentFileNames\":[\"invoice.pdf\"],\"amount\":125.50}"));
        Assert.Equal(HttpStatusCode.OK, validDate.StatusCode);
        service.Verify(s => s.CheckReadinessAsync(It.Is<ApprovalAiReadinessInput>(i =>
            i.RequiredApprovalDate == new DateTime(2026, 9, 30) && i.Amount == 125.50m &&
            i.DocumentFileNames!.Single() == "invoice.pdf"), It.IsAny<CancellationToken>()), Times.Once);

        service.Setup(s => s.CheckReadinessAsync(It.IsAny<ApprovalAiReadinessInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("Provider failed"));
        service.Setup(s => s.CheckReadinessAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("Provider failed"));
        service.Setup(s => s.RecommendApproverAsync(It.IsAny<ApprovalAiApproverInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("Provider failed"));
        service.Setup(s => s.RecommendApproverAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("Provider failed"));
        foreach (var route in new[] { "readiness", "recommend-approver" })
        {
            var preview = await client.PostAsync($"/api/ea/approvals/ai/{route}", Json("{}"));
            var saved = await client.PostAsync($"/api/ea/approvals/123/ai/{route}", null);
            Assert.Equal(HttpStatusCode.Conflict, preview.StatusCode);
            Assert.Equal(saved.StatusCode, preview.StatusCode);
            var previewError = JsonNode.Parse(await preview.Content.ReadAsStringAsync())!;
            var savedError = JsonNode.Parse(await saved.Content.ReadAsStringAsync())!;
            foreach (var field in new[] { "status", "title", "detail" })
                Assert.Equal(savedError[field]!.ToJsonString(), previewError[field]!.ToJsonString());
        }
        await app.StopAsync();
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
