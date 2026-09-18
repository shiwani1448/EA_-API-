namespace Jarvis5.Entities.EaFms;

/// <summary>
/// Delegation: the central execution layer for work delegated to a doer. Source modules
/// (Meeting, Travel, EA Approval, ...) create/finalize a Delegation when delegated work
/// arises from them; after creation, Delegation owns assignment, due date, execution
/// status, and lifecycle independently of the source module.
///
/// Step 1 (this entity + EF config + sequence + migration) is persistence-foundation
/// only — no create/assign/start/complete service or API exists yet.
/// </summary>
public class Delegation
{
    public long Id { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public long EaTaskId { get; set; }
    public EaTask EaTask { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Stable/opaque doer identifier supplied by the caller until EmployeeLookup
    // integration exists. NameSnapshot is display-only, never the canonical identity.
    public string AssignedToId { get; set; } = string.Empty;
    public string? AssignedToNameSnapshot { get; set; }

    // Always resolved server-side from ICurrentUserService; never frontend-supplied.
    public string AssignedById { get; set; } = string.Empty;
    public string? AssignedByNameSnapshot { get; set; }

    // Validated against active PriorityLevel.Name at write time (Meeting's convention),
    // stored as the canonical name string rather than a numeric FK.
    public string? Priority { get; set; }

    public DateTime? DueDate { get; set; }

    // Persisted execution lifecycle only: Pending | InProgress | Completed.
    // DueToday/Overdue are time-derived read views, never persisted here.
    public string Status { get; set; } = "Pending";

    // WHY this work exists: the originating module and record. SourceBusinessModuleId
    // reuses the existing BusinessModule catalog rather than a free-text module name.
    // Nullable: a directly/manually created Delegation has no originating module or
    // record — that is NOT the same thing as originating from the Delegation module
    // itself, so it must never default to Delegation's own BusinessModule.
    public long? SourceBusinessModuleId { get; set; }
    public BusinessModule? SourceBusinessModule { get; set; }
    // Stable identity of the originating record (e.g. TravelRequest.Id, Meeting.Id as string).
    // Nullable for the same reason as SourceBusinessModuleId.
    public string? SourceEntityId { get; set; }
    // Display/business context only (e.g. a ReferenceNo) — never used as relational identity.
    public string? SourceReference { get; set; }

    public string? AdditionalNotes { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedById { get; set; }
    public string? CompletedByNameSnapshot { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
