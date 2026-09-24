using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Mapping;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// Followup/Escalation AI (reminder draft+send, escalation suggestion+apply, resolution-time
/// prediction, at-risk check). Reads/writes go through the REAL FollowupService/
/// EscalationService/FollowupCycleRepository (not mocked), same "prove genuine reuse"
/// convention the other AI test suites use. IClaudeClient and IEaReminderEmailSender are
/// mocked — no real Anthropic API call or SMTP send is ever made from this test suite.
/// </summary>
public class FollowupAiServiceTests
{
    private static readonly DateTime Base = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static readonly IMapper Mapper =
        new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private sealed class Harness
    {
        public required EaFmsDbContext Db { get; init; }
        public required IFollowupService Followups { get; init; }
        public required IEscalationService Escalations { get; init; }
        public required IFollowupCycleRepository Cycles { get; init; }
        public required Mock<IEaReminderEmailSender> ReminderSender { get; init; }
    }

    private static Harness NewHarness(EaFmsDbContext db)
    {
        var currentUser = Mock.Of<ICurrentUserService>(u => u.UserId == 7L && u.UserName == "EA User");
        var audit = Mock.Of<IAuditService>();
        var followups = new FollowupService(new FollowupRepository(db), db, Mapper, currentUser, audit, new FollowupSourceResolver(db));
        var escalations = new EscalationService(new EscalationRepository(db), db, Mapper, currentUser, audit);
        var cycles = new FollowupCycleRepository(db);
        return new Harness { Db = db, Followups = followups, Escalations = escalations, Cycles = cycles, ReminderSender = new Mock<IEaReminderEmailSender>() };
    }

    private static (Mock<IClaudeClient> Claude, Mock<IFollowupAiPromptBuilder> Prompts) Mocks()
    {
        var claude = new Mock<IClaudeClient>();
        var prompts = new Mock<IFollowupAiPromptBuilder>();
        prompts.Setup(p => p.BuildReminderSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildReminderUserPrompt(It.IsAny<FollowupResponseDto>())).Returns("user");
        prompts.Setup(p => p.BuildEscalationSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildEscalationUserPrompt(It.IsAny<FollowupResponseDto>(), It.IsAny<List<(int, string, string, int)>>(), It.IsAny<int>(), It.IsAny<int?>())).Returns("user");
        prompts.Setup(p => p.BuildResolutionExplanationSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildResolutionExplanationUserPrompt(It.IsAny<FollowupResponseDto>(), It.IsAny<string>(), It.IsAny<DateTime>())).Returns("user");
        prompts.Setup(p => p.BuildAtRiskSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildAtRiskUserPrompt(It.IsAny<FollowupResponseDto>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>())).Returns("user");
        return (claude, prompts);
    }

    private static IServiceProvider MakeServiceProvider(IClaudeClient claude)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude);
        return sp.Object;
    }

    private static FollowupAiService Service(Harness h, Mock<IClaudeClient> claude, Mock<IFollowupAiPromptBuilder> prompts, int maxRetries = 2) =>
        new(h.Db, MakeServiceProvider(claude.Object), prompts.Object, h.Followups, h.Escalations, h.Cycles,
            new FollowupAiRepository(h.Db), h.ReminderSender.Object,
            NullLogger<FollowupAiService>.Instance, Options.Create(new ClaudeOptions { MaxRetries = maxRetries }));

    private static async Task<Followup> AddFollowupAsync(EaFmsDbContext db, string? type = "Reminder",
        string? reminderEmail = "recipient@example.com", DateTime? dueAt = null, DateTime? completedAt = null, DateTime? createdDate = null)
    {
        var followup = new Followup
        {
            Subject = "Vendor contract sign-off", Type = type,
            DueAt = dueAt ?? Base.AddDays(-2), CompletedAt = completedAt,
            ReminderRecipientEmail = reminderEmail,
            CreatedBy = "seed", CreatedDate = createdDate ?? Base,
        };
        db.Followups.Add(followup);
        await db.SaveChangesAsync();
        return followup;
    }

    private static async Task<EscalationLevel> AddLevelAsync(EaFmsDbContext db, string code, string name, int level)
    {
        var l = new EscalationLevel { Code = code, Name = name, Level = level, CreatedBy = "seed", CreatedDate = Base };
        db.EscalationLevels.Add(l);
        await db.SaveChangesAsync();
        return l;
    }

    // ============================================================
    // Reminder draft + send
    // ============================================================

    [Fact]
    public async Task SuggestReminder_ReturnsAiDraft_AndLogsSuggestion()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"subject":"Follow-up: Vendor contract sign-off","body":"Checking in on this — it's overdue.","reasoning":"Overdue item, polite nudge."}""");

        var result = await Service(h, claude, prompts).SuggestReminderAsync(followup.Id);

        Assert.Equal("Follow-up: Vendor contract sign-off", result.SuggestedSubject);
        Assert.NotNull(result.SuggestedBody);
        Assert.Equal(1, await db.FollowupReminderSuggestions.CountAsync());
    }

    [Fact]
    public async Task SuggestReminder_MissingFollowup_ThrowsNotFound()
    {
        await using var db = NewDb();
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(h, claude, prompts).SuggestReminderAsync(999));
    }

    [Fact]
    public async Task SendSuggestedReminder_NoRecipientEmail_Throws()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db, reminderEmail: null);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h, claude, prompts)
            .SendSuggestedReminderAsync(followup.Id, new SendFollowupReminderRequestDto { Subject = "x", Body = "y" }));
    }

    [Fact]
    public async Task SendSuggestedReminder_Valid_CallsRealSender_AndMarksLatestSuggestionApplied()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db, reminderEmail: "recipient@example.com");
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"subject":"s","body":"b","reasoning":"r"}""");
        var service = Service(h, claude, prompts);
        await service.SuggestReminderAsync(followup.Id);

        var sent = await service.SendSuggestedReminderAsync(followup.Id, new SendFollowupReminderRequestDto { Subject = "Reviewed subject", Body = "Reviewed body" });

        Assert.Equal("recipient@example.com", sent.RecipientEmail);
        h.ReminderSender.Verify(s => s.SendAsync(
            It.Is<EaReminderEmailMessage>(m => m.To == "recipient@example.com" && m.Subject == "Reviewed subject" && m.Body == "Reviewed body"),
            It.IsAny<CancellationToken>()), Times.Once);

        var suggestion = await db.FollowupReminderSuggestions.SingleAsync();
        Assert.True(suggestion.IsApplied);
        Assert.Equal("recipient@example.com", suggestion.AppliedRecipientEmail);
    }

    // ============================================================
    // Suggest escalation + apply
    // ============================================================

    [Fact]
    public async Task SuggestEscalation_NoLevelsConfigured_ReturnsNullRecommendation_WithoutCallingClaude()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).SuggestEscalationAsync(followup.Id);

        Assert.Null(result.RecommendedEscalationLevelId);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SuggestEscalation_WithCandidates_ReturnsAiPick_AndLogsSuggestion()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db);
        await AddLevelAsync(db, "L1", "EA reminder", 1);
        await AddLevelAsync(db, "L2", "Department Head", 2);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"recommendedLevelCode":"L2","reasoning":"Overdue with no response yet."}""");

        var result = await Service(h, claude, prompts).SuggestEscalationAsync(followup.Id);

        Assert.Equal("Department Head", result.RecommendedEscalationLevelName);
        Assert.Equal(1, await db.FollowupEscalationSuggestions.CountAsync());
    }

    [Fact]
    public async Task SuggestEscalation_AiReturnsCodeNotInCandidates_FallsBackToNull()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db);
        await AddLevelAsync(db, "L1", "EA reminder", 1);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"recommendedLevelCode":"L99","reasoning":"x"}""");

        var result = await Service(h, claude, prompts).SuggestEscalationAsync(followup.Id);

        Assert.Null(result.RecommendedEscalationLevelId);
    }

    [Fact]
    public async Task SuggestEscalation_AlreadyAtHighestLevel_ReturnsNullRecommendation_WithoutCallingClaude()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db);
        var onlyLevel = await AddLevelAsync(db, "L1", "EA reminder", 1);
        db.Escalations.Add(new Escalation { FollowupId = followup.Id, EscalationLevelId = onlyLevel.Id, InitiatedAt = Base, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).SuggestEscalationAsync(followup.Id);

        Assert.Null(result.RecommendedEscalationLevelId);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplySuggestedEscalation_CreatesRealEscalation_AndMarksSuggestionApplied()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db);
        var level = await AddLevelAsync(db, "L1", "EA reminder", 1);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync($$"""{"recommendedLevelCode":"L1","reasoning":"x"}""");
        var service = Service(h, claude, prompts);
        await service.SuggestEscalationAsync(followup.Id);

        var created = await service.ApplySuggestedEscalationAsync(followup.Id, new ApplySuggestedEscalationRequestDto { EscalationLevelId = level.Id, Notes = "Escalating" });

        Assert.True(created.Id > 0);
        Assert.Equal(1, await db.Escalations.CountAsync());
        var suggestion = await db.FollowupEscalationSuggestions.SingleAsync();
        Assert.True(suggestion.IsApplied);
        Assert.Equal(created.Id, suggestion.AppliedEscalationId);
    }

    // ============================================================
    // Predict resolution time
    // ============================================================

    [Fact]
    public async Task PredictResolutionTime_AlreadyCompleted_Throws()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db, completedAt: Base);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h, claude, prompts).PredictResolutionTimeAsync(followup.Id));
    }

    [Fact]
    public async Task PredictResolutionTime_NoHistory_ReturnsNoneBasis()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db, type: "Reminder");
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();

        var result = await Service(h, claude, prompts).PredictResolutionTimeAsync(followup.Id);

        Assert.Equal("None", result.Basis);
        Assert.Null(result.PredictedResolutionDate);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PredictResolutionTime_WithHistoricalSamples_ReturnsAverageBasedDate()
    {
        await using var db = NewDb();
        var target = await AddFollowupAsync(db, type: "Reminder", createdDate: Base);
        // Historical completed followup of the same type: took exactly 2 days.
        await AddFollowupAsync(db, type: "Reminder", createdDate: Base.AddDays(-10), completedAt: Base.AddDays(-8));
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"explanation":"Based on the historical average for this type."}""");

        var result = await Service(h, claude, prompts).PredictResolutionTimeAsync(target.Id);

        Assert.Equal("HistoricalAverage", result.Basis);
        Assert.Equal(Base.AddDays(2), result.PredictedResolutionDate);
        Assert.Equal("Based on the historical average for this type.", result.Explanation);
    }

    // ============================================================
    // At-risk check
    // ============================================================

    [Fact]
    public async Task CheckAtRisk_AlreadyCompleted_Throws()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db, completedAt: Base);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(h, claude, prompts).CheckAtRiskAsync(followup.Id));
    }

    [Fact]
    public async Task CheckAtRisk_ReturnsAiRiskLevel_AndLogsCheck()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"riskLevel":"High","reasoning":"Overdue with no escalation yet.","suggestedAction":"Escalate to the next level."}""");

        var result = await Service(h, claude, prompts).CheckAtRiskAsync(followup.Id);

        Assert.Equal("High", result.RiskLevel);
        Assert.Equal("Escalate to the next level.", result.SuggestedAction);
        Assert.Equal(1, await db.FollowupAtRiskChecks.CountAsync());
    }

    [Fact]
    public async Task CheckAtRisk_AiReturnsInvalidLevel_FallsBackToMedium()
    {
        await using var db = NewDb();
        var followup = await AddFollowupAsync(db);
        var h = NewHarness(db);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"riskLevel":"Critical","reasoning":"x","suggestedAction":"y"}""");

        var result = await Service(h, claude, prompts).CheckAtRiskAsync(followup.Id);

        Assert.Equal("Medium", result.RiskLevel);
    }
}
