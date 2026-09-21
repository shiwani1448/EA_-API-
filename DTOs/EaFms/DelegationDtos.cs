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

    /// <summary>Configured TAT snapshot taken at creation (EaTask.AllottedTatMinutes). Null for a Delegation created without a delegationType (no TAT).</summary>
    public int? AllottedTatMinutes { get; set; }

    /// <summary>
    /// Live ACTIVE TAT in minutes, counted from startedAt with paused time excluded. Null before Start and when there is no TAT.
    /// Frozen at the final value once completed.
    /// </summary>
    public int? CurrentTatUsedMinutes { get; set; }

    /// <summary>Final frozen ACTIVE TAT in minutes (EaTask.TatUsedMinutes). Null until completed and when there is no TAT.</summary>
    public int? TatUsedMinutes { get; set; }

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
