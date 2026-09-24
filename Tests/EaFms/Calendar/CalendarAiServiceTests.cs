using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Calendar;

/// <summary>
/// Calendar AI (quick-add free-text parse + apply, conflict-check). Reads/writes go through
/// the REAL CalendarService (not mocked), same "prove genuine reuse" convention the other
/// three AI test suites use. IClaudeClient is mocked — no real Anthropic API call is ever
/// made from this test suite.
/// </summary>
public class CalendarAiServiceTests
{
    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static CalendarService NewCalendar(EaFmsDbContext db) =>
        new(db, Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 1L));

    private static (Mock<IClaudeClient> Claude, Mock<ICalendarAiPromptBuilder> Prompts) Mocks()
    {
        var claude = new Mock<IClaudeClient>();
        var prompts = new Mock<ICalendarAiPromptBuilder>();
        prompts.Setup(p => p.BuildQuickAddSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildQuickAddUserPrompt(It.IsAny<string>(), It.IsAny<DateTime>())).Returns("user");
        prompts.Setup(p => p.BuildConflictSummarySystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildConflictSummaryUserPrompt(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<List<(string, string, string)>>())).Returns("user");
        return (claude, prompts);
    }

    private static IServiceProvider MakeServiceProvider(IClaudeClient claude)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude);
        return sp.Object;
    }

    private static CalendarAiService Service(
        EaFmsDbContext db, CalendarService calendar, Mock<IClaudeClient> claude, Mock<ICalendarAiPromptBuilder> prompts, int maxRetries = 2) =>
        new(db, MakeServiceProvider(claude.Object), prompts.Object, calendar,
            NullLogger<CalendarAiService>.Instance, Options.Create(new ClaudeOptions { MaxRetries = maxRetries }));

    private static readonly DateTime WindowStart = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    // ============================================================
    // Quick add
    // ============================================================

    [Fact]
    public async Task QuickAdd_ValidText_ReturnsAiParse_AndLogsSuggestion()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"title":"Director travel to Chennai","eventType":"Travel","startDateTime":"2026-10-25T00:00:00","endDateTime":"2026-10-26T00:00:00","isAllDay":true,"location":"Chennai","reasoning":"Explicit date range and destination given."}""");

        var result = await Service(db, NewCalendar(db), claude, prompts).QuickAddAsync(new QuickAddCalendarEventRequestDto { Text = "Director travel to Chennai from 25 to 26 Oct" });

        Assert.Equal("Director travel to Chennai", result.SuggestedTitle);
        Assert.Equal(CalendarEventType.Travel, result.SuggestedEventType);
        Assert.True(result.SuggestedIsAllDay);
        Assert.Equal("Chennai", result.SuggestedLocation);
        Assert.NotNull(result.WarningMessage);
        Assert.Equal(1, await db.CalendarQuickAddSuggestions.CountAsync());
    }

    [Fact]
    public async Task QuickAdd_EmptyText_Throws()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Service(db, NewCalendar(db), claude, prompts).QuickAddAsync(new QuickAddCalendarEventRequestDto { Text = "  " }));
    }

    [Fact]
    public async Task QuickAdd_AiReturnsInvalidEventType_FallsBackToNull_NeverInventsACategory()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"title":"Some event","eventType":"NotARealType","reasoning":"Unclear."}""");

        var result = await Service(db, NewCalendar(db), claude, prompts).QuickAddAsync(new QuickAddCalendarEventRequestDto { Text = "something vague" });

        Assert.Null(result.SuggestedEventType);
    }

    [Fact]
    public async Task ApplyQuickAdd_CreatesRealCalendarEvent_AndMarksSuggestionApplied()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"title":"Director travel to Chennai","eventType":"Travel","startDateTime":"2026-10-25T00:00:00","endDateTime":"2026-10-26T00:00:00","isAllDay":true,"location":"Chennai","reasoning":"ok"}""");
        var service = Service(db, NewCalendar(db), claude, prompts);
        await service.QuickAddAsync(new QuickAddCalendarEventRequestDto { Text = "Director travel to Chennai from 25 to 26 Oct" });

        var created = await service.ApplyQuickAddAsync(new ApplyQuickAddSuggestionRequestDto
        {
            Title = "Director travel to Chennai",
            EventType = CalendarEventType.Travel,
            StartDateTime = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc),
            EndDateTime = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            IsAllDay = true,
            Location = "Chennai",
        });

        Assert.True(created.Id > 0);
        Assert.Equal(1, await db.CalendarEvents.CountAsync());

        var suggestion = await db.CalendarQuickAddSuggestions.SingleAsync();
        Assert.True(suggestion.IsApplied);
        Assert.Equal(created.Id, suggestion.AppliedCalendarEventId);
    }

    [Fact]
    public async Task ApplyQuickAdd_InvalidEventType_ThrowsBadRequestException_ViaCalendarServiceValidation()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        var service = Service(db, NewCalendar(db), claude, prompts);

        await Assert.ThrowsAsync<BadRequestException>(() => service.ApplyQuickAddAsync(new ApplyQuickAddSuggestionRequestDto
        {
            Title = "Bad event",
            EventType = "General",
            StartDateTime = DateTime.UtcNow,
        }));
    }

    // ============================================================
    // Conflict check
    // ============================================================

    [Fact]
    public async Task CheckConflicts_NoEvents_ReturnsNoConflicts_WithoutCallingClaude()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();

        var result = await Service(db, NewCalendar(db), claude, prompts)
            .CheckConflictsAsync(new CalendarConflictCheckRequestDto { From = WindowStart, To = WindowEnd });

        Assert.False(result.HasConflicts);
        Assert.Empty(result.Conflicts);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(1, await db.CalendarConflictChecks.CountAsync());
    }

    [Fact]
    public async Task CheckConflicts_OverlappingEvents_DetectsConflict_AndNarratesViaClaude()
    {
        await using var db = NewDb();
        var calendar = NewCalendar(db);
        await calendar.CreateEventAsync(new CreateCalendarEventDto
        {
            Title = "Meeting with SCT team", EventType = CalendarEventType.ClientMeeting,
            StartDateTime = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc),
            EndDateTime = new DateTime(2026, 10, 10, 16, 0, 0, DateTimeKind.Utc),
        });
        await calendar.CreateEventAsync(new CreateCalendarEventDto
        {
            Title = "Director travel to Chennai", EventType = CalendarEventType.Travel,
            StartDateTime = new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc),
            EndDateTime = new DateTime(2026, 10, 10, 18, 0, 0, DateTimeKind.Utc),
        });

        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync("system", "user", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"summary":"The SCT team meeting overlaps with the Chennai travel by one hour."}""");

        var result = await Service(db, calendar, claude, prompts)
            .CheckConflictsAsync(new CalendarConflictCheckRequestDto { From = WindowStart, To = WindowEnd });

        Assert.True(result.HasConflicts);
        Assert.Single(result.Conflicts);
        Assert.Equal("The SCT team meeting overlaps with the Chennai travel by one hour.", result.Summary);
        var logged = await db.CalendarConflictChecks.SingleAsync();
        Assert.True(logged.HasConflicts);
    }

    [Fact]
    public async Task CheckConflicts_NonOverlappingEvents_NoConflicts()
    {
        await using var db = NewDb();
        var calendar = NewCalendar(db);
        await calendar.CreateEventAsync(new CreateCalendarEventDto
        {
            Title = "Morning meeting", EventType = CalendarEventType.InternalMeeting,
            StartDateTime = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc),
            EndDateTime = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc),
        });
        await calendar.CreateEventAsync(new CreateCalendarEventDto
        {
            Title = "Afternoon meeting", EventType = CalendarEventType.InternalMeeting,
            StartDateTime = new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc),
            EndDateTime = new DateTime(2026, 10, 10, 15, 0, 0, DateTimeKind.Utc),
        });

        var (claude, prompts) = Mocks();
        var result = await Service(db, calendar, claude, prompts)
            .CheckConflictsAsync(new CalendarConflictCheckRequestDto { From = WindowStart, To = WindowEnd });

        Assert.False(result.HasConflicts);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckConflicts_FromAfterTo_Throws()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<BadRequestException>(() => Service(db, NewCalendar(db), claude, prompts)
            .CheckConflictsAsync(new CalendarConflictCheckRequestDto { From = WindowEnd, To = WindowStart }));
    }
}
