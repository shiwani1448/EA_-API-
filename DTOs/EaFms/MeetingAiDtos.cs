namespace Jarvis5.Dtos.EaFms;

/// <summary>Response for POST /api/ea/meetings/{meetingId}/ai/analyze. Preview only —
/// nothing behind this response is persisted; it reflects a single on-demand AI read of
/// the Meeting's existing completion evidence.</summary>
public class MeetingAiAnalysisResponseDto
{
    public long MeetingId { get; set; }

    public List<MeetingAiProposedActionDto> ProposedActions { get; set; } = new();

    public bool MomUsed { get; set; }
    public bool PdfUsed { get; set; }

    /// <summary>Set when completion evidence was partially unusable (e.g. the PDF could
    /// not be extracted) but the analysis still proceeded with what was usable.</summary>
    public string? WarningMessage { get; set; }
}

/// <summary>One AI-proposed action point, for EA review only. DoerName is a plain-text
/// name suggestion, never a resolved employee identity — DoerId is intentionally absent
/// here and must be assigned/confirmed later by the EA/frontend.</summary>
public class MeetingAiProposedActionDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? DoerName { get; set; }
    public string? Priority { get; set; }
    public DateTime? DueDate { get; set; }
}

/// <summary>Reviewed manual or AI-proposed actions. MeetingActionId updates an existing
/// undelegated action; null inserts a new action. All submitted items are delegated.</summary>
public class ConfirmMeetingAiActionsRequestDto
{
    public List<CreateMeetingActionDto> Actions { get; set; } = new();
}

/// <summary>Response for POST /api/ea/meetings/{meetingId}/ai/actions/confirm. Reuses the
/// existing MeetingActionDto (the same shape GET .../actions already returns) rather than
/// inventing a parallel AI response shape.</summary>
public class MeetingAiActionsConfirmResponseDto
{
    public int CreatedDelegationCount { get; set; }
    public string? DelegationDecision { get; set; }
    public long MeetingId { get; set; }
    public List<MeetingActionDto> CreatedActions { get; set; } = new();
}
