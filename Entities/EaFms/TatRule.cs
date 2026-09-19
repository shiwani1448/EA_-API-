using System;

namespace Jarvis5.Entities.EaFms;

public class TatRule
{
    public long Id { get; set; }
    public long BusinessModuleId { get; set; }
    public BusinessModule BusinessModule { get; set; } = null!;
    public string ModuleName { get; set; } = null!;
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public int TatMinutes { get; set; }
    public bool IsActive { get; set; }

    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    // Frontend-supplied actor snapshots (not verified by EA); CreatedBy/ModifiedBy keep the display value.
    public string? CreatedByEmployeeId { get; set; }
    public string? CreatedByEmployeeName { get; set; }
    public string? ModifiedByEmployeeId { get; set; }
    public string? ModifiedByEmployeeName { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
