namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI readiness check for an Approval Request, shaped exactly like
/// ApprovalAiReadinessResponseDto. MissingFieldsJson/SuggestedDocumentsJson are jsonb
/// because they are genuinely lists of strings — see MeetingActionExtraction's doc comment.
/// Purely advisory: there is no confirm/apply step for a readiness check in the real flow.
/// </summary>
public class ApprovalReadinessCheck
{
    public long Id { get; set; }
    public long ApprovalRequestId { get; set; }

    public bool IsLikelyReady { get; set; }
    /// <summary>List of field names Claude considers missing/weak.</summary>
    public string MissingFieldsJson { get; set; } = string.Empty;
    /// <summary>List of document types typically expected for this request type.</summary>
    public string SuggestedDocumentsJson { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? WarningMessage { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
