namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI action-extraction result for a Meeting, shaped exactly like
/// MeetingAiAnalysisResponseDto — same "one table per real AI task, with real columns for
/// its real fields" convention SCIH already uses (SCIH_Analysis/SCIH_SolutionDesign each
/// have their own feature-specific columns, not a generic shared shape). Only
/// ProposedActionsJson is jsonb, because it is genuinely a repeating list of structured
/// sub-items — the same reason SCIH's own AnalysisJson/HRMS's MatchedSkillsJson use jsonb
/// for their nested list fields while keeping scalar fields as real columns.
///
/// AppliedActionsJson/IsApplied/AppliedAt track what actually got confirmed into real
/// ea_meeting_actions rows via /ai/actions/confirm — null/false when the EA never confirmed
/// (or typed the actions manually without ever calling /ai/actions/extract first).
/// </summary>
public class MeetingActionExtraction
{
    public long Id { get; set; }
    public long MeetingId { get; set; }

    public bool MomUsed { get; set; }
    public bool PdfUsed { get; set; }
    public string? WarningMessage { get; set; }

    /// <summary>List of { title, description, doerName, priority, dueDate } — see
    /// MeetingAiProposedActionDto.</summary>
    public string ProposedActionsJson { get; set; } = string.Empty;

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    /// <summary>The real ea_meeting_actions rows actually created, once confirmed.</summary>
    public string? AppliedActionsJson { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
