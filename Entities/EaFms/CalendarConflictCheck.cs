namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI conflict-check result over the EA's own real ea_calendar_events rows in a date
/// range — purely advisory, never applied/written anywhere (matching ApprovalReadinessCheck/
/// ApprovalStatusSummary's own "log only" convention). Every conflict pair is detected in C#
/// from real event rows before Claude ever sees them; Claude only phrases the plain-English
/// Summary — it can never invent a conflict that isn't in ConflictsJson.
/// </summary>
public class CalendarConflictCheck
{
    public long Id { get; set; }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool HasConflicts { get; set; }
    /// <summary>List of { firstEventId, firstTitle, secondEventId, secondTitle,
    /// overlapDescription } — see CalendarAiConflictPairDto.</summary>
    public string ConflictsJson { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? WarningMessage { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
