using System;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.AiUsage;

/// <summary>
/// Every EA FMS AI call is stored in ea_ai_usage_logs — who used AI, where, for what task, the
/// prompt, the full response and the tokens — whether or not the EA accepts the result.
/// </summary>
public class EaAiUsageTests
{
    private static DbContextOptions<EaFmsDbContext> NewOptions() => new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static IHttpContextAccessor Http(string path, string? routeKey = null, string? routeValue = null,
        string? headerId = null, string? headerName = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = path;
        if (routeKey is not null) ctx.Request.RouteValues[routeKey] = routeValue;
        if (headerId is not null) ctx.Request.Headers["X-Employee-Id"] = headerId;
        if (headerName is not null) ctx.Request.Headers["X-Employee-Name"] = headerName;
        return new HttpContextAccessor { HttpContext = ctx };
    }

    private static EaAiUsageLogger Logger(DbContextOptions<EaFmsDbContext> options, IHttpContextAccessor http, string? loginId = null, string? loginName = null) =>
        new(options, Jarvis5.Tests.EaFms.Followups.FollowupTestSupport.User(loginId, loginName), http, NullLogger<EaAiUsageLogger>.Instance);

    /// <summary>Stands in for ClaudeClient: fills the opted-in usage exactly like the real stream does.</summary>
    private static Task<string> FakeClaude(string response, long input = 1200, long output = 300)
    {
        var usage = ClaudeUsageCapture.Current!;
        usage.Model = "claude-test"; usage.MessageId = "msg_1"; usage.StopReason = "end_turn";
        usage.InputTokens = input; usage.OutputTokens = output; usage.CacheReadInputTokens = 0; usage.CacheCreationInputTokens = 0;
        return Task.FromResult(response);
    }

    private static async Task SeedDelegationAsync(DbContextOptions<EaFmsDbContext> options)
    {
        await using var db = new EaFmsDbContext(options);
        db.Delegations.Add(new DelegationEntity { Id = 12, ReferenceNo = "DLG-12", EaTaskId = 99, Title = "Prepare deck", DoerId = "E-9",
            AssignedById = "ea", Status = "Pending", CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Call_IsStored_WithWhoWhereForWhatResponseAndTokens()
    {
        var options = NewOptions();
        await SeedDelegationAsync(options);
        var logger = Logger(options, Http("/api/ea/delegations/12/ai/delay-risk", "delegationId", "12", headerId: "S5I-1013", headerName: "Siddhi Jadhav"));

        var text = await logger.CallAsync(EaAiModules.Delegation, "Delegation delay risk check", "system prompt", "user prompt", 1,
            () => FakeClaude("""{"riskLevel":"Low"}"""), default);

        Assert.Equal("""{"riskLevel":"Low"}""", text);
        await using var db = new EaFmsDbContext(options);
        var row = await db.AiUsageLogs.SingleAsync();
        Assert.Equal(("Delegation", "Delegation delay risk check", "POST /api/ea/delegations/12/ai/delay-risk"), (row.Module, row.Feature, row.Endpoint));
        Assert.Equal(("12", (long?)99), (row.BusinessRecordId, row.EaTaskId));                 // for what: the record and its task
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (row.RequestedById, row.RequestedByName));   // who (no login: frontend headers)
        Assert.Equal(("system prompt", "user prompt", """{"riskLevel":"Low"}"""), (row.SystemPrompt, row.UserPrompt, row.ResponseText));
        Assert.Equal(("Succeeded", 1, "claude-test", "msg_1", "end_turn"), (row.Status, row.AttemptNo, row.Model, row.MessageId, row.StopReason));
        Assert.Equal(((long?)1200, (long?)300, (long?)1500), (row.InputTokens, row.OutputTokens, row.TotalTokens));
        Assert.True(row.CompletedAt >= row.RequestedAt);
        Assert.Null(ClaudeUsageCapture.Current);   // capture is cleaned up after the call
    }

    [Fact]
    public async Task TheLogin_WinsOverTheHeaders()
    {
        var options = NewOptions();
        var logger = Logger(options, Http("/api/ea/calendar/ai/quick-add", headerId: "spoof", headerName: "Spoof"), loginId: "S5I-1", loginName: "Jay Pujari");

        await logger.CallAsync(EaAiModules.Calendar, "Calendar quick add", "s", "u", 1, () => FakeClaude("{}"), default);

        await using var db = new EaFmsDbContext(options);
        var row = await db.AiUsageLogs.SingleAsync();
        Assert.Equal(("S5I-1", "Jay Pujari", (string?)null), (row.RequestedById, row.RequestedByName, row.BusinessRecordId));
    }

    [Fact]
    public async Task AFailedCall_IsStoredAsFailed_AndTheErrorStillReachesTheCaller()
    {
        var options = NewOptions();
        var logger = Logger(options, Http("/api/ea/meetings/5/ai/analyze", "meetingId", "5"));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => logger.CallAsync(EaAiModules.Meeting, "Meeting action extraction", "s", "u", 1,
            () => throw new BusinessRuleException("AI analysis service (Claude) returned an error: overloaded"), default));

        await using var db = new EaFmsDbContext(options);
        var row = await db.AiUsageLogs.SingleAsync();
        Assert.Equal(("Failed", ex.Message, (string?)null), (row.Status, row.ErrorMessage, row.ResponseText));
    }

    [Fact]
    public async Task AnInvalidJsonReply_IsMarked_AndTheRetryIsStoredAsItsOwnRow()
    {
        var options = NewOptions();
        var logger = Logger(options, Http("/api/ea/approvals/ai/readiness"));

        await logger.CallAsync(EaAiModules.Approval, "Approval readiness check", "s", "u", 1, () => FakeClaude("not json"), default);
        await logger.MarkLastInvalidJsonAsync("AI returned invalid JSON.");
        await logger.CallAsync(EaAiModules.Approval, "Approval readiness check", "s", "u retry", 2, () => FakeClaude("{}"), default);

        await using var db = new EaFmsDbContext(options);
        var rows = await db.AiUsageLogs.OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(new[] { ("InvalidJson", 1), ("Succeeded", 2) }, rows.Select(r => (r.Status, r.AttemptNo)));
        Assert.Equal("not json", rows[0].ResponseText);   // the bad reply is kept too
    }

    // ------------------------------------------------------------------ read side

    private static async Task<EaFmsDbContext> SeedLogsAsync()
    {
        var options = NewOptions();
        var db = new EaFmsDbContext(options);
        EaAiUsageLog Log(string module, string feature, string? record, long? task, string who, long input, long output, string status = "Succeeded", int day = 24) => new()
        {
            Module = module, Feature = feature, BusinessRecordId = record, EaTaskId = task, RequestedById = who, RequestedByName = who == "S5I-1" ? "Jay Pujari" : "Riya",
            RequestedAt = new DateTime(2026, 9, day, 6, 0, 0, DateTimeKind.Utc), CompletedAt = new DateTime(2026, 9, day, 6, 0, 2, DateTimeKind.Utc),
            DurationMs = 2000, AttemptNo = 1, Status = status, InputTokens = input, OutputTokens = output, TotalTokens = input + output,
            CreatedDate = new DateTime(2026, 9, day, 6, 0, 2, DateTimeKind.Utc)
        };
        db.AiUsageLogs.AddRange(
            Log("Delegation", "Delegation delay risk check", "12", 99, "S5I-1", 1000, 200),
            Log("Delegation", "Delegation owner suggestion", "12", 99, "S5I-1", 800, 100),
            Log("Delegation", "Delegation delay risk check", "12", 99, "S5I-1", 500, 0, status: "InvalidJson"),
            Log("Meeting", "Meeting action extraction", "5", 50, "S5I-2", 3000, 900, day: 25),
            Log("Calendar", "Calendar quick add", null, null, "S5I-1", 100, 50, day: 25));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Summary_GivesTokensPerTaskModuleFeatureEaAndDay()
    {
        await using var db = await SeedLogsAsync();

        var s = await new EaAiUsageService(db).SummaryAsync(new EaAiUsageQueryDto(), default);

        Assert.Equal((5, 4, 1, 6650L), (s.Total.Calls, s.Total.Succeeded, s.Total.InvalidJson, s.Total.TotalTokens));
        var task = s.ByTask.Single(t => t.EaTaskId == 99);
        Assert.Equal(("Delegation", "12", 3, 2600L), (task.Module, task.BusinessRecordId, task.Calls, task.TotalTokens));
        Assert.Equal(2, s.ByTask.Count);   // the calendar quick-add has no task
        Assert.Equal(("Meeting", 3900L), (s.ByModule[0].Key, s.ByModule[0].TotalTokens));   // most tokens first
        Assert.Equal(2750L, s.ByEmployee.Single(e => e.Key == "Jay Pujari").TotalTokens);
        Assert.Equal(new[] { "2026-09-24", "2026-09-25" }, s.ByDay.Select(d => d.Key));
    }

    [Fact]
    public async Task ForTask_ListsEveryAiCallOnTheTask_WithTotals()
    {
        await using var db = await SeedLogsAsync();

        var t = await new EaAiUsageService(db).ForTaskAsync(99, default);

        Assert.Equal((3, 2600L), (t.Total.Calls, t.Total.TotalTokens));
        Assert.Equal(3, t.Calls.Count);
        Assert.Equal(2, t.ByFeature.Count);
    }

    [Fact]
    public async Task List_Filters_ByEaModuleStatus_AndDetailShowsTheFullResponse()
    {
        await using var db = await SeedLogsAsync();
        var svc = new EaAiUsageService(db);

        var mine = await svc.ListAsync(new EaAiUsageQueryDto { EmployeeName = " jay  PUJARI " }, default);
        Assert.Equal(4, mine.TotalCount);
        var invalid = await svc.ListAsync(new EaAiUsageQueryDto { Module = "delegation", Status = "InvalidJson" }, default);
        Assert.Single(invalid.Items);

        var id = (await db.AiUsageLogs.FirstAsync()).Id;
        var row = await db.AiUsageLogs.SingleAsync(r => r.Id == id);
        row.ResponseText = """{"riskLevel":"Low"}"""; row.UserPrompt = "prompt";
        await db.SaveChangesAsync();
        var detail = await svc.GetAsync(id, default);
        Assert.Equal(("""{"riskLevel":"Low"}""", "prompt"), (detail.ResponseText, detail.UserPrompt));

        await Assert.ThrowsAsync<BadRequestException>(() => svc.ListAsync(new EaAiUsageQueryDto { Module = "Payroll" }, default));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync(999_999, default));
    }

    // ------------------------------------------------------------------ "which response did the EA use"

    private static EaAiUsageLog Stored(string module, string feature, string? record, string who, DateTime at, string status = "Succeeded") => new()
    {
        Module = module, Feature = feature, BusinessRecordId = record, RequestedById = who, RequestedByName = who,
        RequestedAt = at, CompletedAt = at, Status = status, AttemptNo = 1, ResponseText = "{}", CreatedDate = at,
    };

    [Fact]
    public async Task Apply_MarksTheLatestSuccessfulResponseForThatRecord_AsUsed_WithWhatAndWho()
    {
        var options = NewOptions();
        await using (var db = new EaFmsDbContext(options))
        {
            db.AiUsageLogs.AddRange(
                Stored("Delegation", "Delegation owner suggestion", "12", "S5I-1", new DateTime(2026, 9, 25, 5, 0, 0, DateTimeKind.Utc)),
                Stored("Delegation", "Delegation owner suggestion", "12", "S5I-1", new DateTime(2026, 9, 25, 6, 0, 0, DateTimeKind.Utc)),
                Stored("Delegation", "Delegation owner suggestion", "12", "S5I-1", new DateTime(2026, 9, 25, 7, 0, 0, DateTimeKind.Utc), status: "InvalidJson"),
                Stored("Delegation", "Delegation owner suggestion", "99", "S5I-1", new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc)));
            await db.SaveChangesAsync();
        }
        var logger = Logger(options, Http("/api/ea/delegations/12/ai/suggest-owner/apply", headerId: "S5I-1", headerName: "Jay Pujari"));

        await logger.MarkUsedAsync(EaAiModules.Delegation, ["Delegation owner suggestion"], "12", new { doerId = "E-9", doerName = "Riya" });

        await using var check = new EaFmsDbContext(options);
        var rows = await check.AiUsageLogs.OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(new[] { false, true, false, false }, rows.Select(r => r.IsUsed));      // the 06:00 reply (latest successful for record 12)
        var used = rows[1];
        Assert.Equal(("S5I-1", "Jay Pujari", """{"doerId":"E-9","doerName":"Riya"}"""), (used.UsedById, used.UsedByName, used.UsedValue));
        Assert.NotNull(used.UsedAt);
    }

    [Fact]
    public async Task Apply_WithoutARecord_MarksThatEasLatestResponse()
    {
        var options = NewOptions();
        await using (var db = new EaFmsDbContext(options))
        {
            db.AiUsageLogs.AddRange(
                Stored("Calendar", "Calendar quick add", null, "S5I-1", new DateTime(2026, 9, 25, 5, 0, 0, DateTimeKind.Utc)),
                Stored("Calendar", "Calendar quick add", null, "S5I-2", new DateTime(2026, 9, 25, 6, 0, 0, DateTimeKind.Utc)));
            await db.SaveChangesAsync();
        }
        var logger = Logger(options, Http("/api/ea/calendar/ai/quick-add/apply", headerId: "S5I-1"));

        await logger.MarkUsedAsync(EaAiModules.Calendar, ["Calendar quick add"], null, new { calendarEventId = 5 });

        await using var check = new EaFmsDbContext(options);
        Assert.Equal(new[] { true, false }, await check.AiUsageLogs.OrderBy(r => r.Id).Select(r => r.IsUsed).ToListAsync());
    }

    [Fact]
    public async Task Apply_WhenAiWasNeverAsked_MarksNothing()
    {
        var options = NewOptions();
        var logger = Logger(options, Http("/api/ea/delegations/12/ai/suggest-owner/apply"));

        await logger.MarkUsedAsync(EaAiModules.Delegation, ["Delegation owner suggestion"], "12", new { doerId = "E-9" });

        await using var check = new EaFmsDbContext(options);
        Assert.False(await check.AiUsageLogs.AnyAsync());
    }

    [Fact]
    public async Task EveryAiResponse_CarriesItsLogId_AndTheScreenCanReportUse()
    {
        var options = NewOptions();
        var http = Http("/api/ea/travel/requests/7/ai/itinerary", "travelRequestId", "7", headerName: "Siddhi Jadhav");
        var logger = Logger(options, http);

        await logger.CallAsync(EaAiModules.Travel, "Travel itinerary draft", "s", "u", 1, () => FakeClaude("{\"itinerary\":\"Day 1\"}"), default);
        var id = long.Parse(http.HttpContext!.Response.Headers["X-AI-Usage-Id"].ToString());

        Assert.True(await logger.MarkUsedByIdAsync(id, "Copied the itinerary into the travel note"));
        Assert.False(await logger.MarkUsedByIdAsync(999_999, null));

        await using var db = new EaFmsDbContext(options);
        var row = await db.AiUsageLogs.SingleAsync();
        Assert.Equal((id, true, "Siddhi Jadhav", "Copied the itinerary into the travel note"), (row.Id, row.IsUsed, row.UsedByName, row.UsedValue));
    }

    [Fact]
    public async Task Summary_And_List_ShowUsedAndNotUsed()
    {
        await using var db = await SeedLogsAsync();
        var first = await db.AiUsageLogs.OrderBy(r => r.Id).FirstAsync();
        first.IsUsed = true; first.UsedAt = DateTime.UtcNow; first.UsedByName = "Jay Pujari"; first.UsedValue = "{\"riskLevel\":\"Low\"}";
        await db.SaveChangesAsync();
        var svc = new EaAiUsageService(db);

        var s = await svc.SummaryAsync(new EaAiUsageQueryDto(), default);
        Assert.Equal((1, 3), (s.Total.Used, s.Total.NotUsed));   // 4 successful, 1 used
        Assert.Equal((1, 1), (s.ByTask.Single(t => t.EaTaskId == 99).Used, s.ByTask.Single(t => t.EaTaskId == 99).NotUsed));

        var used = await svc.ListAsync(new EaAiUsageQueryDto { Used = true }, default);
        Assert.Equal((1, true, "Jay Pujari"), (used.TotalCount, used.Items[0].IsUsed, used.Items[0].UsedByName));
        Assert.Equal("{\"riskLevel\":\"Low\"}", (await svc.GetAsync(first.Id, default)).UsedValue);
        Assert.Equal(4, (await svc.ListAsync(new EaAiUsageQueryDto { Used = false }, default)).TotalCount);
    }
}
