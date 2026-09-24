namespace Jarvis5.Dtos.EaFms;

public sealed class ApprovalListItemDto { public long ApprovalRequestId { get; init; } public long EaTaskId { get; init; } public string ReferenceNo { get; init; } = string.Empty; public string? RequestTitle { get; init; } public string? Description { get; init; } public string? RequestedBy { get; init; } public string? Department { get; init; } public string? Type { get; init; } public string? Priority { get; init; } public string? Approver { get; init; } public string? ApprovedBy { get; init; } public string? RejectedBy { get; init; } public DateTime? RequiredApprovalDate { get; init; } public string? WorkflowStatus { get; init; } public string DueState { get; init; } = "Unavailable"; public int CurrentCycleNo { get; init; } public int DocumentCount { get; init; } public DateTime CreatedAt { get; init; } public DateTime? SubmittedAt { get; init; } public DateTime? UpdatedAt { get; init; } /* Central Task Review/Rework state (Phase 1), separate from WorkflowStatus/CurrentCycleNo above */ public TaskReviewSummaryDto ReviewSummary { get; init; } = new(); /* Per-phase TAT (Actual/Review/Rework) — see ApprovalPhaseTat's own doc comment. Mirrors the current phase's own numbers; see ApprovalQueryService for the exact mapping. */ public List<ApprovalPhaseTatDto> PhaseTat { get; init; } = new(); public int? AllottedTatMinutes { get; init; } public int? TatUsedMinutes { get; init; } public int? TatPausedMinutes { get; init; } public MeetingTatSummaryDto TatSummary { get; init; } = new(); public bool IsPaused { get; init; } }
public sealed class ApprovalListResponseDto { public IReadOnlyList<ApprovalListItemDto> Items { get; init; } = Array.Empty<ApprovalListItemDto>(); public int TotalCount { get; init; } public int Page { get; init; } public int PageSize { get; init; } }
public sealed class ApprovalCycleDto { public long Id { get; init; } public int CycleNo { get; init; } public DateTime? SubmittedAt { get; init; } public string? SubmittedBy { get; init; } public DateTime? RequiredApprovalDate { get; init; } public string? ApproverId { get; init; } public string? Status { get; init; } public string? ChangeReason { get; init; } public string? DecisionComment { get; init; } public DateTime CreatedAt { get; init; } public DateTime? UpdatedAt { get; init; } }
public sealed class ApprovalHistoryItemDto { public long Id { get; init; } public string EventType { get; init; } = string.Empty; public DateTime OccurredAt { get; init; } public string? ActorId { get; init; } public string? ActorName { get; init; } public string? Description { get; init; } }
public sealed class ApprovalTaskDto { public long EaTaskId { get; init; } public long BusinessModuleId { get; init; } public string BusinessRecordId { get; init; } = string.Empty; public string? Status { get; init; } public string? Priority { get; init; } public DateTime? DueDate { get; init; } public int? AllottedTatMinutes { get; init; } public string DueState { get; init; } = "Unavailable"; }
public sealed class ApprovalDetailDto { public long ApprovalRequestId { get; init; } public long EaTaskId { get; init; } public string ReferenceNo { get; init; } = string.Empty; public string? RequestTitle { get; init; } public string? Description { get; init; } public string? Justification { get; init; } public string? RequestedBy { get; init; } public string? CreatedBy { get; init; } public string? UpdatedBy { get; init; } public string? Type { get; init; } public string? Priority { get; init; } public string? Department { get; init; } public decimal? Amount { get; init; } public string? Currency { get; init; } public string? Approver { get; init; } public string? ApprovedBy { get; init; } public string? RejectedBy { get; init; } public string? WorkflowStatus { get; init; } public DateTime? RequiredApprovalDate { get; init; } public DateTime CreatedAt { get; init; } public DateTime? UpdatedAt { get; init; } public DateTime? SubmittedAt { get; init; } public DateTime? ApprovedAt { get; init; } public DateTime? RejectedAt { get; init; } public DateTime? ClosedAt { get; init; } public string DueState { get; init; } = "Unavailable"; public ApprovalTaskDto Task { get; init; } = new(); public int CurrentCycleNo { get; init; } public ApprovalCycleDto? CurrentCycle { get; init; } public ApprovalCycleDto? LatestCycle { get; init; } public IReadOnlyList<ApprovalCycleDto> Cycles { get; init; } = Array.Empty<ApprovalCycleDto>(); public IReadOnlyList<ApprovalDocumentResponseDto> Documents { get; init; } = Array.Empty<ApprovalDocumentResponseDto>(); public IReadOnlyList<ApprovalHistoryItemDto> History { get; init; } = Array.Empty<ApprovalHistoryItemDto>(); /* Central Task Review/Rework state (Phase 1), separate from WorkflowStatus/CurrentCycleNo/Cycles above */ public TaskReviewSummaryDto ReviewSummary { get; init; } = new(); /* Per-phase TAT (Actual/Review/Rework) — see ApprovalPhaseTat's own doc comment. Mirrors the current phase's own numbers; see ApprovalQueryService for the exact mapping. */ public List<ApprovalPhaseTatDto> PhaseTat { get; init; } = new(); public int? AllottedTatMinutes { get; init; } public int? TatUsedMinutes { get; init; } public int? TatPausedMinutes { get; init; } public MeetingTatSummaryDto TatSummary { get; init; } = new(); public bool IsPaused { get; init; } }
public sealed class ApprovalDashboardDto { public int TotalRequests { get; init; } public int PendingApproval { get; init; } public int Approved { get; init; } public int Rejected { get; init; } public int Overdue { get; init; } }

/// <summary>
/// One TAT phase of an Approval's lifecycle — see ApprovalPhaseTat's own doc comment for the full
/// model. Structurally identical to DelegationPhaseTatDto. TatUsedMinutes/TatPausedMinutes/PauseCount/
/// TatDifferenceMinutes are null only when no TAT Rule is configured for this phase's (RequestType,
/// TaskType); PauseTime/PauseCount still reflect real pause activity even then.
/// </summary>
public sealed class ApprovalPhaseTatDto { public string TaskType { get; init; } = string.Empty; public int ReviewCycleNumber { get; init; } public DateTime? StartedAt { get; init; } public DateTime? EndedAt { get; init; } public int? AllottedTatMinutes { get; init; } public int? TatUsedMinutes { get; init; } public int? TatPausedMinutes { get; init; } public int? PauseCount { get; init; } public int? TatDifferenceMinutes { get; init; } public decimal? TatUsedSeconds { get; init; } public decimal? TatPausedSeconds { get; init; } public decimal? TatDifferenceSeconds { get; init; } }

/// <summary>Optional body for POST /api/ea/approvals/{approvalRequestId}/pause. Same shape/convention as DelegationPauseRequestDto.</summary>
public sealed class ApprovalPauseRequestDto { [System.ComponentModel.DataAnnotations.MaxLength(2000)] public string? PauseReason { get; init; } }

/// <summary>
/// multipart/form-data body for POST /api/ea/approvals/{approvalRequestId}/review/approve. The optional
/// attachment lets the assignee attach their own document to the approval — same convention as
/// DelegationApproveReviewRequestDto, just stored under Approval's own Content/ApprovalReview folder.
/// </summary>
public class ApprovalApproveReviewRequestDto
{
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewRemark { get; set; }

    /// <summary>Optional attachment (.pdf, application/pdf, max 25 MiB, must be a readable PDF).</summary>
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "attachment")]
    public Microsoft.AspNetCore.Http.IFormFile? Attachment { get; set; }
}

/// <summary>
/// multipart/form-data body for POST /api/ea/approvals/{approvalRequestId}/review/rework. Same
/// convention as DelegationRequestReworkRequestDto.
/// </summary>
public class ApprovalRequestReworkRequestDto
{
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReworkRemark { get; set; }

    /// <summary>Optional attachment (.pdf, application/pdf, max 25 MiB, must be a readable PDF).</summary>
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "attachment")]
    public Microsoft.AspNetCore.Http.IFormFile? Attachment { get; set; }
}
