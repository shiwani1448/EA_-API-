namespace Jarvis5.Entities.EaFms;

/// <summary>
/// IntakeRequest: represents an incoming EA FMS request submitted to the system.
/// Minimal fields based on EA FMS roadmap/ERD guidance.
/// </summary>
public class IntakeRequest
{
    public long Id { get; set; }

    // Short title for the request.
    public string Title { get; set; } = string.Empty;

    // Optional longer description/details of the request.
    public string? Description { get; set; }

    // Optional associations to existing EA FMS catalogs.
    public long? BusinessModuleId { get; set; }
    public BusinessModule? BusinessModule { get; set; }

    public int? StatusId { get; set; }
    public Status? Status { get; set; }

    public int? PriorityLevelId { get; set; }
    public PriorityLevel? PriorityLevel { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    // Source / provenance
    public string? Source { get; set; }
    public string? SourceChannel { get; set; }
    public string? SourceReferenceId { get; set; }

    // Optional required-by date for the intake request (frontend expects RequiredDate)
    public DateTime? RequiredDate { get; set; }

    // Confidential flag
    public bool IsConfidential { get; set; } = false;

    // Assignment (owner/doer) fields - external identity strings (no FK to HRMS)
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    // Navigation
    public ICollection<IntakeClassification> Classifications { get; set; } = new List<IntakeClassification>();
}
