namespace Jarvis5.Entities.EaFms;

/// <summary>
/// BusinessModule: top-level catalog of business modules registered in EA FMS.
/// Minimal fields implemented to keep the model extensible.
/// </summary>
public class BusinessModule
{
    public long Id { get; set; }

    // Display name of the module (e.g. "Finance", "Legal").
    public string Name { get; set; } = string.Empty;

    // Optional longer description.
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    // Frontend-supplied actor snapshots (not verified by EA); CreatedBy/ModifiedBy keep the display value.
    public string? CreatedByEmployeeId { get; set; }
    public string? CreatedByEmployeeName { get; set; }
    public string? ModifiedByEmployeeId { get; set; }
    public string? ModifiedByEmployeeName { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
