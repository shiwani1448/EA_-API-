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
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string AssignedToId { get; set; } = string.Empty;
    public string? AssignedToNameSnapshot { get; set; }

    public DateTime? DueDate { get; set; }
    public string? Priority { get; set; }

    public long SourceBusinessModuleId { get; set; }
    public string SourceEntityId { get; set; } = string.Empty;
    public string? SourceReference { get; set; }

    public string? AdditionalNotes { get; set; }
}

/// <summary>
/// Editable business fields only. Id/ReferenceNo/EaTaskId/AssignedBy/Status/
/// StartedAt/CompletedAt/CompletedBy/CreatedAt remain server-owned and immutable here.
/// </summary>
public class DelegationUpdateRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string AssignedToId { get; set; } = string.Empty;
    public string? AssignedToNameSnapshot { get; set; }

    public DateTime? DueDate { get; set; }
    public string? Priority { get; set; }

    public long SourceBusinessModuleId { get; set; }
    public string SourceEntityId { get; set; } = string.Empty;
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

    public string AssignedToId { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }

    public string AssignedById { get; set; } = string.Empty;
    public string? AssignedByName { get; set; }

    public string? Priority { get; set; }
    public DateTime? DueDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public long SourceBusinessModuleId { get; set; }
    public string SourceModuleName { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public string? SourceReference { get; set; }

    public string? AdditionalNotes { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedById { get; set; }
    public string? CompletedByName { get; set; }

    /// <summary>Server-computed, not persisted: Status != Completed AND DueDate's calendar date == today.</summary>
    public bool IsDueToday { get; set; }
    /// <summary>Server-computed, not persisted: Status != Completed AND DueDate's calendar date &lt; today.</summary>
    public bool IsOverdue { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ============================================================
// LIST / REGISTER QUERY
// ============================================================

public class DelegationListQueryDto
{
    /// <summary>Case-insensitive match across ReferenceNo, Title, AssignedToNameSnapshot, SourceReference.</summary>
    public string? Search { get; set; }

    public string? AssignedToId { get; set; }
    public string? Priority { get; set; }
    /// <summary>Persisted status filter: Pending, InProgress, or Completed.</summary>
    public string? Status { get; set; }
    public long? SourceBusinessModuleId { get; set; }
    /// <summary>Exact business calendar-date match (day boundary), not a range.</summary>
    public DateTime? DueDate { get; set; }
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
