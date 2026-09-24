namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI approver recommendation for an Approval Request, shaped exactly like
/// ApprovalAiApproverSuggestionResponseDto — every field here is scalar, so no jsonb is
/// needed at all. Applied via /ai/recommend-approver/apply, which writes ApproverId/
/// ApproverName onto the real ea_approval_requests row — AppliedApproverId/
/// AppliedApproverName record exactly what was written (the EA's reviewed/edited choice,
/// which may differ from RecommendedApproverName if they typed something else).
/// </summary>
public class ApprovalApproverRecommendation
{
    public long Id { get; set; }
    public long ApprovalRequestId { get; set; }

    /// <summary>Null when there was no history to recommend from.</summary>
    public string? RecommendedApproverName { get; set; }
    public int HistoricalSampleSize { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? AppliedApproverId { get; set; }
    public string? AppliedApproverName { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
