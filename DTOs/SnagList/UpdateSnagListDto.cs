namespace Jarvis5.Dtos.SnagList;

/// <summary>Body for PATCH /api/snaglist/{id} - partial update: only fields present
/// (non-null) in the request are changed, everything else keeps its current value.
/// Field names mirror SnagListDetailDto/CreateSnagListDto. Audit/delete fields are
/// accepted for schema symmetry but ignored; the server owns those values.
/// CurrentStatus accepts Open/In Progress/Completed but never Closed - closing only
/// ever happens via POST /api/snaglist/{id}/close, which enforces the mandatory-stage
/// business rules. StageDetails: omit/null to leave stages untouched; if provided,
/// it replaces the full stage list exactly as sent.</summary>
public class UpdateSnagListDto
{
    public long? RequestId { get; set; }
    public long? TaskId { get; set; }
    public string? Module { get; set; }
    public string? SnagDescription { get; set; }
    public string? Priority { get; set; }
    public string? CurrentStatus { get; set; }
    public List<object>? StageDetails { get; set; }

    /// <summary>Ignored - immutable once created.</summary>
    public DateTime CreationDate { get; set; }

    /// <summary>Ignored - immutable once created.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Ignored - server always sets this to the current UTC time.</summary>
    public DateTime? UpdationDate { get; set; }

    /// <summary>Ignored - server always sets this from the acting user.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Ignored - soft delete is not changed by this endpoint.</summary>
    public string IsDelete { get; set; } = "false";

    /// <summary>Ignored - soft delete is not changed by this endpoint.</summary>
    public string? IsDeletedBy { get; set; }
}
