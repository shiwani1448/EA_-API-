using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class SaveTatRuleDto
{
    public long ModuleId { get; set; }
    public string? ModuleName { get; set; }
    [System.ComponentModel.DataAnnotations.Required]
    public string? Type { get; set; }
    [System.ComponentModel.DataAnnotations.Required]
    public string? Subtype { get; set; }
    public int TatMinutes { get; set; }
    // Nullable so omission cannot silently become false.
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DefaultValue(true)]
    public bool? IsActive { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee id). Stored as attribution; not verified.</summary>
    public string? EmployeeId { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee name). Stored as attribution; not verified.</summary>
    public string? EmployeeName { get; set; }
}

public class TatRuleDto
{
    public long Id { get; set; }
    public long ModuleId { get; set; }
    public string ModuleName { get; set; } = null!;
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public int TatMinutes { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedByEmployeeId { get; set; }
    public string? CreatedByEmployeeName { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModifiedByEmployeeId { get; set; }
    public string? ModifiedByEmployeeName { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
