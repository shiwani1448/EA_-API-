namespace Jarvis5.Dtos.EaFms;

// ============================================================
// Readiness check (preview only — no persistence, nothing written)
// ============================================================

/// <summary>Response for POST /api/ea/approvals/{approvalRequestId}/ai/readiness.
/// Judges completeness from the request's own fields and the uploaded documents' FILE
/// NAMES only — there is no OCR/text-extraction pipeline wired to Approval documents, so
/// this can never verify what a document actually contains.</summary>
public class ApprovalAiReadinessResponseDto
{
    public long? ApprovalRequestId { get; set; }
    public bool IsLikelyReady { get; set; }

    /// <summary>Request fields Claude considers missing/weak (e.g. no justification, no
    /// required approval date) — derived only from fields that are actually null/blank.</summary>
    public List<string> MissingFields { get; set; } = new();

    /// <summary>Document types typically expected for this RequestType that don't appear
    /// to be present, based only on the uploaded file name list — never a claim about what
    /// is or isn't inside any uploaded file.</summary>
    public List<string> SuggestedDocuments { get; set; } = new();
    public string? Notes { get; set; }
    public string? WarningMessage { get; set; }
}

// ============================================================
// Approver suggestion (preview only — advisory; no employee/role directory exists)
// ============================================================

/// <summary>Response for POST /api/ea/approvals/{approvalRequestId}/ai/recommend-approver.
/// Purely a pattern read from past Approved requests in the same department — this system
/// has no employee/role directory, so this can never be an authorization rule and never
/// names anyone who wasn't already a real approver of a past request.</summary>
public class ApprovalAiApproverSuggestionResponseDto
{
    public long? ApprovalRequestId { get; set; }

    /// <summary>Null when there is no approval history to draw from for this department —
    /// deliberately never guessed in that case.</summary>
    public string? RecommendedApproverName { get; set; }

    /// <summary>How many past approved requests in the same department this is based on.
    /// 0 means RecommendedApproverName is null and Reasoning explains there's no history.</summary>
    public int HistoricalSampleSize { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>Request for POST /api/ea/approvals/{approvalRequestId}/ai/recommend-approver/apply.
/// The EA has reviewed the suggestion (or typed their own choice) and wants it written onto
/// the real request — this never re-calls Claude, it only persists the value given here.</summary>
public class ApplyApproverSuggestionRequestDto
{
    public string? ApproverId { get; set; }
    public string ApproverName { get; set; } = string.Empty;
}

// ============================================================
// Status summary (preview only — pure narrative over data the request already has)
// ============================================================

/// <summary>Response for POST /api/ea/approvals/{approvalRequestId}/ai/status-summary.
/// A plain-English narrative built only from this request's own cycles/history/due-state —
/// introduces no new facts.</summary>
public class ApprovalAiStatusSummaryResponseDto
{
    public long ApprovalRequestId { get; set; }
    public string? Summary { get; set; }
    public string? WorkflowStatus { get; set; }
    public int CurrentCycleNo { get; set; }
    public string? DueState { get; set; }
}

/// <summary>Optional form fields for a read-only approver preview.</summary>
public class ApprovalAiApproverInput
{
    public string? RequestType { get; set; }
    public string? Department { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }

    internal long? SavedRequestId { get; set; }
    internal string? SavedPriority { get; set; }
}

/// <summary>Optional form fields; documents are represented by file names only.</summary>
public class ApprovalAiReadinessInput
{
    public string? RequestTitle { get; set; }
    public string? RequestType { get; set; }
    public string? Priority { get; set; }
    public string? Department { get; set; }
    public string? Description { get; set; }
    public string? Justification { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime? RequiredApprovalDate { get; set; }
    public string? ApproverName { get; set; }
    public List<string>? DocumentFileNames { get; set; } = new();

    internal long? SavedRequestId { get; set; }
    internal string? WorkflowStatus { get; set; }
    internal int CurrentCycleNo { get; set; }
    internal ApprovalCycleDto? LatestCycle { get; set; }
}
