using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// AI assistance for the standalone Calendar module (quick-add, conflict-check). Orchestration
/// only — provider-specific logic (Claude, JSON parsing) lives in the already-registered
/// shared services; this class never talks to Claude directly. Reuses ICalendarService for
/// every actual read/write against ea_calendar_events rather than querying EaFmsDbContext
/// directly, so this module has exactly one place that owns Calendar data access.
/// </summary>
public class CalendarAiService : ICalendarAiService
{
    private static readonly string[] ValidEventTypes =
        { CalendarEventType.ClientMeeting, CalendarEventType.InternalMeeting, CalendarEventType.Personal, CalendarEventType.Travel };

    private readonly EaFmsDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICalendarAiPromptBuilder _promptBuilder;
    private readonly ICalendarService _calendar;
    private readonly ILogger<CalendarAiService> _logger;
    private readonly int _maxAiAttempts;

    // IClaudeClient is resolved lazily via IServiceProvider (not a direct constructor
    // dependency) — same reason as every other AI service: IClaudeClient is a singleton
    // whose constructor throws if AnthropicSettings:ApiKey is missing, and this service must
    // not be forced to fail to construct in an environment without a configured key.
    public CalendarAiService(
        EaFmsDbContext db,
        IServiceProvider serviceProvider,
        ICalendarAiPromptBuilder promptBuilder,
        ICalendarService calendar,
        ILogger<CalendarAiService> logger,
        IOptions<ClaudeOptions> claudeOptions)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _promptBuilder = promptBuilder;
        _calendar = calendar;
        _logger = logger;
        _maxAiAttempts = Math.Clamp(claudeOptions.Value.MaxRetries, 1, 3);
    }

    public async Task<CalendarAiQuickAddResponseDto> QuickAddAsync(QuickAddCalendarEventRequestDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
            throw new BusinessRuleException("Text must not be empty.");

        var nowLocal = IndiaBusinessCalendar.Today;
        var systemPrompt = _promptBuilder.BuildQuickAddSystemPrompt();
        var userPrompt = _promptBuilder.BuildQuickAddUserPrompt(dto.Text, nowLocal);

        var aiResult = await GenerateAndParseAsync<CalendarAiQuickAddResultDto>(systemPrompt, userPrompt, "Calendar quick add", ct);

        // Defensive: Claude may only ever pick a type from the real four — never a category
        // outside CalendarEventType, and never a guess if it returned something else.
        var eventType = ValidEventTypes.FirstOrDefault(t => string.Equals(t, aiResult.EventType, StringComparison.OrdinalIgnoreCase));

        var response = new CalendarAiQuickAddResponseDto
        {
            SuggestedTitle = string.IsNullOrWhiteSpace(aiResult.Title) ? null : aiResult.Title.Trim(),
            SuggestedEventType = eventType,
            SuggestedStartDateTime = aiResult.StartDateTime,
            SuggestedEndDateTime = aiResult.EndDateTime,
            SuggestedIsAllDay = aiResult.IsAllDay,
            SuggestedLocation = string.IsNullOrWhiteSpace(aiResult.Location) ? null : aiResult.Location.Trim(),
            Reasoning = aiResult.Reasoning,
            WarningMessage = "A parse of your text only — review every field before applying; nothing is created on the calendar yet.",
        };
        await AiSuggestionWriters.LogCalendarQuickAddSuggestionAsync(_db, dto.Text, response, ct);
        return response;
    }

    public async Task<CalendarEventDto> ApplyQuickAddAsync(ApplyQuickAddSuggestionRequestDto dto, CancellationToken ct = default)
    {
        // Writes exactly the fields the caller sent — never re-derives or re-calls Claude, so
        // this is safe whether the EA accepted the AI's exact suggestion or retyped every
        // field herself. CalendarService.CreateEventAsync owns the actual field validation.
        var created = await _calendar.CreateEventAsync(new CreateCalendarEventDto
        {
            Title = dto.Title,
            EventType = dto.EventType,
            StartDateTime = dto.StartDateTime,
            EndDateTime = dto.EndDateTime,
            IsAllDay = dto.IsAllDay,
            Location = dto.Location,
            Description = dto.Description,
            OrganizerEmployeeId = dto.OrganizerEmployeeId,
            OrganizerName = dto.OrganizerName,
            Notes = dto.Notes,
        }, ct);
        await AiSuggestionWriters.MarkCalendarQuickAddSuggestionAppliedAsync(_db, created.Id, ct);
        await _serviceProvider.MarkAiResponseUsedAsync(EaAiModules.Calendar, ["Calendar quick add"], null,
            new { calendarEventId = created.Id, created.Title, created.EventType, created.StartDateTime, created.EndDateTime }, ct);
        return created;
    }

    public async Task<CalendarAiConflictCheckResponseDto> CheckConflictsAsync(CalendarConflictCheckRequestDto dto, CancellationToken ct = default)
    {
        if (dto.From > dto.To)
            throw new BadRequestException("From must be on or before To.");

        var events = await _calendar.GetEventsAsync(
            new CalendarEventsQueryDto { From = dto.From, To = dto.To, IncludeCompleted = true }, ct);

        // Overlaps computed here in plain C# from real event rows, never invented by Claude —
        // same "computed here, never invented" rule the Approval/Delegation historical-stats
        // queries already follow. A timed event with no EndDateTime is assumed to run one
        // hour; an all-day event with no EndDateTime is assumed to span its own calendar day.
        var conflicts = new List<CalendarAiConflictPairDto>();
        for (var i = 0; i < events.Count; i++)
        {
            var a = events[i];
            var aEnd = EffectiveEnd(a);
            for (var j = i + 1; j < events.Count; j++)
            {
                var b = events[j];
                var bEnd = EffectiveEnd(b);
                if (a.StartDateTime < bEnd && b.StartDateTime < aEnd)
                {
                    conflicts.Add(new CalendarAiConflictPairDto
                    {
                        FirstEventId = a.Id,
                        FirstTitle = a.Title,
                        SecondEventId = b.Id,
                        SecondTitle = b.Title,
                        OverlapDescription = $"{a.StartDateTime:yyyy-MM-dd HH:mm}-{aEnd:HH:mm} overlaps {b.StartDateTime:yyyy-MM-dd HH:mm}-{bEnd:HH:mm}",
                    });
                }
            }
        }

        const string warning = "Detected from exact start/end times on your own calendar entries only — a timed event with no end time set is assumed to run one hour.";

        if (conflicts.Count == 0)
        {
            // No point calling Claude to narrate an empty list — same short-circuit the
            // Approval/Delegation AI services use when there is nothing to reason about.
            var clearResponse = new CalendarAiConflictCheckResponseDto
            {
                From = dto.From,
                To = dto.To,
                HasConflicts = false,
                Conflicts = conflicts,
                Summary = "No overlapping events were found in this date range.",
                WarningMessage = warning,
            };
            await AiSuggestionWriters.LogCalendarConflictCheckAsync(_db, clearResponse, ct);
            return clearResponse;
        }

        var systemPrompt = _promptBuilder.BuildConflictSummarySystemPrompt();
        var userPrompt = _promptBuilder.BuildConflictSummaryUserPrompt(
            dto.From, dto.To, conflicts.Select(c => (c.FirstTitle, c.SecondTitle, c.OverlapDescription)).ToList());
        var aiResult = await GenerateAndParseAsync<CalendarAiConflictSummaryResultDto>(
            systemPrompt, userPrompt, "Calendar conflict summary", ct);

        var response = new CalendarAiConflictCheckResponseDto
        {
            From = dto.From,
            To = dto.To,
            HasConflicts = true,
            Conflicts = conflicts,
            Summary = aiResult.Summary,
            WarningMessage = warning,
        };
        await AiSuggestionWriters.LogCalendarConflictCheckAsync(_db, response, ct);
        return response;
    }

    private static DateTime EffectiveEnd(CalendarEventDto e) =>
        e.EndDateTime ?? (e.IsAllDay ? e.StartDateTime.Date.AddDays(1) : e.StartDateTime.AddHours(1));

    // Same retry-on-malformed-JSON pattern as MeetingAiService/TravelAiService/ApprovalAiService/DelegationAiService.
    private async Task<T> GenerateAndParseAsync<T>(
        string systemPrompt,
        string userPrompt,
        string entityName,
        CancellationToken ct) where T : new()
    {
        BusinessRuleException? lastParseError = null;

        for (var attempt = 1; attempt <= _maxAiAttempts; attempt++)
        {
            var attemptPrompt = attempt == 1
                ? userPrompt
                : userPrompt + """

                    IMPORTANT RETRY: The prior response was not valid JSON. Return one complete, compact JSON
                    object only. Do not use markdown fences, comments, smart quotes, trailing commas, or text
                    before/after the object.
                    """;

            var claudeClient = _serviceProvider.GetRequiredService<IClaudeClient>();
            // Every EA AI call is recorded in ea_ai_usage_logs (who, where, for what, prompt, response, tokens).
            var aiUsage = _serviceProvider.GetService<IEaAiUsageLogger>();
            var rawResponse = aiUsage is null
                ? await claudeClient.GenerateJsonAsync(systemPrompt, attemptPrompt, ct)
                : await aiUsage.CallAsync(EaAiModules.Calendar, entityName, systemPrompt, attemptPrompt, attempt,
                    () => claudeClient.GenerateJsonAsync(systemPrompt, attemptPrompt, ct), ct);

            try
            {
                return AiJsonResponseParser.Parse<T>(rawResponse, _logger, entityName);
            }
            catch (BusinessRuleException ex)
            {
                if (aiUsage is not null) await aiUsage.MarkLastInvalidJsonAsync(ex.Message);
                if (attempt >= _maxAiAttempts) throw;
                lastParseError = ex;
                _logger.LogWarning(
                    "AI {Entity} returned invalid JSON on attempt {Attempt}/{MaxAttempts}; retrying.",
                    entityName, attempt, _maxAiAttempts);
            }
        }

        throw lastParseError
            ?? new BusinessRuleException($"AI {entityName} service did not return valid JSON.");
    }
}

// Internal shapes for parsing Claude's raw JSON only — never returned from the public API.
internal class CalendarAiQuickAddResultDto
{
    public string? Title { get; set; }
    public string? EventType { get; set; }
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public bool IsAllDay { get; set; }
    public string? Location { get; set; }
    public string? Reasoning { get; set; }
}

internal class CalendarAiConflictSummaryResultDto
{
    public string? Summary { get; set; }
}
