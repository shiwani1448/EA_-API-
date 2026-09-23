namespace Jarvis5.Dtos.EaFms;

// ============================================================
// CREATE / UPDATE REQUEST
// ============================================================

/// <summary>
/// Fields the frontend may supply when creating a Delegation.
/// Backend-owned fields (Id, ReferenceNo, EaTaskId, AssignedById, AssignedByNameSnapshot,
/// Status, StartedAt, CompletedAt, CompletedById, CompletedByNameSnapshot, audit
/// timestamps) are NOT present here.
/// </summary>
public class DelegationCreateRequestDto
{
    // Frontend-owned form fields — nullable so ASP.NET Core's implicit-required
    // validation for non-nullable reference types (from [ApiController]) does not
    // silently re-impose requiredness the service deliberately does not enforce
    // (CreateCoreAsync already coalesces a missing value to "" rather than throwing).
    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Frontend-supplied free-text classification (e.g. "Self Delegation"). Trimmed; blank stored as null; max 200. No enum or catalog.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? DelegationType { get; set; }

    /// <summary>Stable/opaque identifier of the person doing the work.</summary>
    public string? DoerId { get; set; }
    /// <summary>Display-only doer name snapshot.</summary>
    public string? DoerNameSnapshot { get; set; }

    /// <summary>PLANNED/business start date. Not the actual execution timestamp (startedAt) and does not gate /start.</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>PLANNED/business end date (persisted in the DueDate column).</summary>
    public DateTime? EndDate { get; set; }
    public string? Priority { get; set; }

    // Origin of the delegated work — null means a direct/manual Delegation with no
    // originating module or record. When supplied, it must resolve to a real,
    // active BusinessModule (never Delegation's own module, never fabricated).
    public long? SourceBusinessModuleId { get; set; }
    public string? SourceEntityId { get; set; }
    public string? SourceReference { get; set; }

    public string? AdditionalNotes { get; set; }
}

/// <summary>
/// Editable business fields only. Id/ReferenceNo/EaTaskId/AssignedBy/Status/
/// StartedAt/CompletedAt/CompletedBy/CreatedAt remain server-owned and immutable here.
/// </summary>
public class DelegationUpdateRequestDto
{
    // See DelegationCreateRequestDto — same implicit-required rationale.
    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Frontend-supplied free-text classification (e.g. "Self Delegation"). Trimmed; blank stored as null; max 200. No enum or catalog.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? DelegationType { get; set; }

    /// <summary>Stable/opaque identifier of the person doing the work.</summary>
    public string? DoerId { get; set; }
    /// <summary>Display-only doer name snapshot.</summary>
    public string? DoerNameSnapshot { get; set; }

    /// <summary>PLANNED/business start date. Not the actual execution timestamp (startedAt) and does not gate /start.</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>PLANNED/business end date (persisted in the DueDate column).</summary>
    public DateTime? EndDate { get; set; }
    public string? Priority { get; set; }

    // See DelegationCreateRequestDto — same nullable-source semantics.
    public long? SourceBusinessModuleId { get; set; }
    public string? SourceEntityId { get; set; }
    public string? SourceReference { get; set; }

    public string? AdditionalNotes { get; set; }
}

// ============================================================
// RESPONSE
// ============================================================

public class DelegationResponseDto
{
    public long DelegationId { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    /// <summary>Returned for traceability only — never the frontend's Delegation identity.</summary>
    public long EaTaskId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? DelegationType { get; set; }

    /// <summary>The doer's identifier (Delegation.DoerId).</summary>
    public string DoerId { get; set; } = string.Empty;
    /// <summary>The doer's display name snapshot (Delegation.DoerNameSnapshot).</summary>
    public string? DoerName { get; set; }

    public string AssignedById { get; set; } = string.Empty;
    public string? AssignedByName { get; set; }

    /// <summary>Planned/business start date. Distinct from startedAt (actual execution start).</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>Planned/business end date (Delegation.DueDate). Drives isDueToday / isOverdue.</summary>
    public DateTime? EndDate { get; set; }
    public string? Priority { get; set; }

    public string Status { get; set; } = string.Empty;

    public long? SourceBusinessModuleId { get; set; }
    public string? SourceModuleName { get; set; }
    public string? SourceEntityId { get; set; }
    public string? SourceReference { get; set; }

    public string? AdditionalNotes { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    /// <summary>ea_attachments.Id of the completion PDF uploaded with the Complete request, or null when none was uploaded.</summary>
    public long? CompletionPdfAttachmentId { get; set; }

    /// <summary>
    /// Server-derived, never persisted: true only while the Delegation is InProgress and has an open WorkPause.
    /// Status stays InProgress while paused; Resume closes the pause.
    /// </summary>
    public bool IsPaused { get; set; }

    // ---- Execution / TAT snapshot: read from the central EaTask (ea_tasks) and its WorkPauses; nothing here is stored on
    // ea_delegations. Names follow the central EaTask API (GET /api/ea/tasks/{id}). Unavailable TAT is null, never 0. ----

    /// <summary>Central task execution status: NotStarted | InProgress | Completed. Stays InProgress while paused (see isPaused).</summary>
    public string ExecutionStatus { get; set; } = string.Empty;

    /// <summary>Configured TAT snapshot taken at creation (EaTask.AllottedTatMinutes). Null for a Delegation created without a delegationType (no TAT). Present on Meeting's own detail contract under the same name.</summary>
    public int? AllottedTatMinutes { get; set; }

    /// <summary>
    /// Strict Meeting-aligned public TAT value — same property, same formula and same lifecycle semantics
    /// as Meeting's own tatUsedMinutes (TatSummaryCalculator's elapsed-minus-paused result, echoed in whole
    /// minutes). Null before Start and when there is no TAT (no delegationType); live while InProgress
    /// (including while paused, where it holds steady rather than advancing); stable once Completed because
    /// tatSummary's end anchor becomes completedAt. There is no separate "current" vs "frozen" public field —
    /// Meeting has only one, and Delegation's public contract now matches it exactly. (The central EaTask
    /// still keeps its own internal frozen TatUsedMinutes column for the EaTask API and EM Report — that is
    /// a different, internal concern and is untouched by this field.)
    /// </summary>
    public int? TatUsedMinutes { get; set; }

    /// <summary>
    /// Meeting-style live simple-pause minutes for the current TAT window — same TatSummaryCalculator
    /// formula and WorkPause data as Meeting's own tatPausedMinutes. Null before Start and when there is no
    /// TAT (no delegationType); 0 once started with no pauses; stops advancing while paused; stable once Completed.
    /// </summary>
    public int? TatPausedMinutes { get; set; }

    /// <summary>
    /// Meeting-style full-precision TAT summary — the exact same MeetingTatSummaryDto type, property names,
    /// types and TatSummaryCalculator formula as Meeting's own tatSummary (tat/totalTat/tatDifference/
    /// startTime/endTime/lastActiveTime/pauseTime/pauseCount). Populated from before Start (tat: zero) once a
    /// delegationType configures a TAT. For a Delegation with no delegationType (no TAT), tat/totalTat/
    /// tatDifference use Meeting's own "unavailable" shape (null/zero/zero) — the same fallback Meeting
    /// itself falls back to for a task with no configured TAT — while startTime/endTime/lastActiveTime/
    /// pauseTime/pauseCount still reflect the Delegation's real execution timeline. Never persisted.
    /// </summary>
    public MeetingTatSummaryDto TatSummary { get; set; } = new();

    /// <summary>
    /// Central Task Review/Rework state for this Delegation's EaTask (Phase 1). Null-shaped
    /// (status null, reviewCycleNumber 0) when never submitted for review. Entirely separate
    /// from executionStatus/status above — Phase 1 does not gate or trigger either from this.
    /// </summary>
    public TaskReviewSummaryDto ReviewSummary { get; set; } = new();

    /// <summary>
    /// Every TAT phase this Delegation has been through, oldest first: exactly one Actual (the
    /// doer's original work), then a Review/Rework pair per review cycle. Each phase has its own
    /// Allotted/Used/Paused/PauseCount, resolved against its own (DelegationType, TaskType) TAT
    /// Rule — see DelegationPhaseTat. The last entry is the current phase if the Delegation is not
    /// yet Completed (endedAt null, live-ticking values); every other entry is frozen.
    /// </summary>
    public List<DelegationPhaseTatDto> PhaseTat { get; set; } = new();

    /// <summary>Server-computed, not persisted: Status != Completed AND endDate's calendar date == today.</summary>
    public bool IsDueToday { get; set; }
    /// <summary>Server-computed, not persisted: Status != Completed AND endDate's calendar date &lt; today.</summary>
    public bool IsOverdue { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ============================================================
// LIST / REGISTER QUERY
// ============================================================

public class DelegationListQueryDto
{
    /// <summary>Case-insensitive match across ReferenceNo, Title, DoerNameSnapshot, SourceReference.</summary>
    public string? Search { get; set; }

    /// <summary>Exact doer match (Delegation.DoerId).</summary>
    public string? DoerId { get; set; }
    /// <summary>Case-insensitive exact match on delegationType.</summary>
    public string? DelegationType { get; set; }
    public string? Priority { get; set; }
    /// <summary>Persisted status filter: Pending, InProgress, or Completed.</summary>
    public string? Status { get; set; }
    public long? SourceBusinessModuleId { get; set; }
    /// <summary>Exact business calendar-date match (day boundary) on the planned end date, not a range.</summary>
    public DateTime? EndDate { get; set; }
    /// <summary>all | pending | inProgress | dueToday | overdue | completed.</summary>
    public string? View { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// ============================================================
// KPI SUMMARY
// ============================================================

/// <summary>
/// Register KPI card counts. All values are computed on demand from active
/// (non-deleted) Delegations — none are persisted.
/// </summary>
public class DelegationSummaryResponseDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int InProgress { get; set; }
    public int DueToday { get; set; }
    public int Overdue { get; set; }
    public int Completed { get; set; }
}

/// <summary>
/// multipart/form-data body for POST /api/ea/delegations/{delegationId}/complete, following the same
/// pattern as MeetingCompleteRequestDto. The completion PDF is optional for a Delegation
/// (unlike Meeting): the Delegation completes without it.
/// </summary>
public class DelegationCompleteRequestDto
{
    /// <summary>
    /// Optional completion PDF (.pdf, application/pdf, max 25 MiB, must be a readable PDF).
    /// Omit to complete without a PDF.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "completionPdf")]
    public Microsoft.AspNetCore.Http.IFormFile? CompletionPdf { get; set; }
}

/// <summary>
/// Optional body for POST /api/ea/delegations/{delegationId}/pause. Same reason concept as Meeting's pause request.
/// </summary>
public class DelegationPauseRequestDto
{
    /// <summary>Optional pause reason (max 2000). Defaults to "Delegation paused" when omitted or blank.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(2000)]
    public string? PauseReason { get; set; }
}

/// <summary>
/// multipart/form-data body for POST /api/ea/delegations/{delegationId}/review/approve. The optional
/// attachment lets the assignee attach their own document (sign-off notes, an annotated file, etc.)
/// to the approval — separate from the doer's own completion PDF uploaded at Complete/submit time.
/// </summary>
public class DelegationApproveReviewRequestDto
{
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewRemark { get; set; }

    /// <summary>Optional attachment (.pdf, application/pdf, max 25 MiB, must be a readable PDF).</summary>
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "attachment")]
    public Microsoft.AspNetCore.Http.IFormFile? Attachment { get; set; }
}

/// <summary>
/// multipart/form-data body for POST /api/ea/delegations/{delegationId}/review/rework. The optional
/// attachment lets the assignee attach their own document (marked-up feedback, a reference file)
/// explaining what needs to be redone.
/// </summary>
public class DelegationRequestReworkRequestDto
{
    public string? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReworkRemark { get; set; }

    /// <summary>Optional attachment (.pdf, application/pdf, max 25 MiB, must be a readable PDF).</summary>
    [Microsoft.AspNetCore.Mvc.FromForm(Name = "attachment")]
    public Microsoft.AspNetCore.Http.IFormFile? Attachment { get; set; }
}

/// <summary>
/// One TAT phase of a Delegation's lifecycle — see DelegationPhaseTat's own doc comment for the
/// full model. TatUsedMinutes/TatPausedMinutes/PauseCount/TatDifferenceMinutes are null only when
/// no TAT Rule is configured for this phase's (DelegationType, TaskType); PauseTime/PauseCount
/// still reflect real pause activity even then, the same "still meaningful without a budget"
/// convention the whole-delegation TatSummary already uses.
/// </summary>
public class DelegationPhaseTatDto
{
    /// <summary>Actual | Review | Rework.</summary>
    public string TaskType { get; set; } = string.Empty;
    /// <summary>0 for Actual; the review cycle number for Review/Rework.</summary>
    public int ReviewCycleNumber { get; set; }
    public DateTime StartedAt { get; set; }
    /// <summary>Null when this is the current, still-open phase (live-ticking values).</summary>
    public DateTime? EndedAt { get; set; }
    public int? AllottedTatMinutes { get; set; }
    public int? TatUsedMinutes { get; set; }
    public int? TatPausedMinutes { get; set; }
    public int? PauseCount { get; set; }
    /// <summary>AllottedTatMinutes - TatUsedMinutes; null whenever either side is null.</summary>
    public int? TatDifferenceMinutes { get; set; }

    /// <summary>Decimal seconds, computed live or frozen on closure. Null without a budget or for legacy snapshots.</summary>
    public decimal? TatUsedSeconds { get; set; }
    /// <summary>Decimal paused seconds, including sub-minute pauses. Null for legacy closed snapshots.</summary>
    public decimal? TatPausedSeconds { get; set; }
    /// <summary>AllottedTatMinutes * 60 - TatUsedSeconds at the same calculation instant; may be negative.</summary>
    public decimal? TatDifferenceSeconds { get; set; }
}
