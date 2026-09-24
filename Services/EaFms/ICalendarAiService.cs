using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// AI assistance for the standalone Calendar module: quick-add (free text -> structured
/// event, suggest+apply) and conflict-check (preview-only overlap detection). Orchestration
/// only — provider-specific logic (Claude, JSON parsing) lives in the already-registered
/// shared services; this class never talks to Claude directly, and conflict-check never
/// invents an overlap that wasn't computed from real ea_calendar_events rows.
/// </summary>
public interface ICalendarAiService
{
    Task<CalendarAiQuickAddResponseDto> QuickAddAsync(QuickAddCalendarEventRequestDto dto, CancellationToken ct = default);
    Task<CalendarEventDto> ApplyQuickAddAsync(ApplyQuickAddSuggestionRequestDto dto, CancellationToken ct = default);
    Task<CalendarAiConflictCheckResponseDto> CheckConflictsAsync(CalendarConflictCheckRequestDto dto, CancellationToken ct = default);
}
