using System.Text;

namespace Jarvis5.Services.EaFms;

public class CalendarAiPromptBuilder : ICalendarAiPromptBuilder
{
    // ============================================================
    // Quick add
    // ============================================================

    public string BuildQuickAddSystemPrompt() => """
        You help an Executive Assistant turn one line of free text into a structured calendar
        event for her Director's calendar (like Google Calendar's "quick add").

        Strict rules:
        - "eventType" must be exactly one of: ClientMeeting, InternalMeeting, Personal, Travel
          — or null if the text doesn't give you enough to confidently pick one. Never invent
          a category outside this list.
        - Resolve relative dates ("tomorrow", "next Monday", "25th") against the CURRENT DATE
          given below. If no date at all is mentioned, return null for the start date rather
          than guessing one.
        - If the text names a date range (e.g. "from 25 to 26 Oct") with no time of day, treat
          it as an all-day event (isAllDay: true) spanning start-of-day to start-of-day-after.
        - If a specific time of day is given but no end time, leave endDateTime null.
        - Never invent a location that isn't stated or clearly implied by a named place.
        - Reasoning must be one short sentence explaining how you read the text.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildQuickAddUserPrompt(string text, DateTime nowLocal)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CURRENT DATE: {nowLocal:yyyy-MM-dd} ({nowLocal:dddd})");
        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("TEXT TO PARSE:");
        sb.AppendLine(text);
        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""
            {
              "title": string|null,
              "eventType": string|null,
              "startDateTime": string|null,
              "endDateTime": string|null,
              "isAllDay": boolean,
              "location": string|null,
              "reasoning": string
            }
            """);
        sb.AppendLine("Dates must be ISO 8601 (e.g. \"2026-10-25T14:30:00\").");
        return sb.ToString();
    }

    // ============================================================
    // Conflict summary — narrates a list of REAL, already-detected overlaps
    // ============================================================

    public string BuildConflictSummarySystemPrompt() => """
        You write a short, plain-English heads-up for a busy Executive Assistant about
        scheduling overlaps already found on her Director's calendar.

        Strict rules:
        - The CONFLICTS list below is the complete, final set of overlaps — already detected
          by exact time-range comparison. Never add, remove, or invent a conflict that isn't
          in the list, and never claim there are no conflicts if the list is non-empty.
        - Keep it to two or three sentences, referencing only the titles and times given.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildConflictSummaryUserPrompt(DateTime from, DateTime to, List<(string FirstTitle, string SecondTitle, string OverlapDescription)> conflicts)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Date range: {from:yyyy-MM-dd} to {to:yyyy-MM-dd}");
        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("CONFLICTS (already detected — do not add or remove any)");
        foreach (var c in conflicts)
            sb.AppendLine($"- \"{c.FirstTitle}\" overlaps \"{c.SecondTitle}\": {c.OverlapDescription}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "summary": string }""");
        return sb.ToString();
    }
}
