using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SaveBusinessModuleDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Frontend-supplied actor snapshot (operator's employee id). Stored as attribution; not verified.</summary>
    public string? EmployeeId { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee name). Stored as attribution; not verified.</summary>
    public string? EmployeeName { get; set; }
}

public sealed class BusinessModuleDto
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public string? CreatedBy { get; init; }
    public string? CreatedByEmployeeId { get; init; }
    public string? CreatedByEmployeeName { get; init; }
    public DateTime CreatedDate { get; init; }
    public string? ModifiedBy { get; init; }
    public string? ModifiedByEmployeeId { get; init; }
    public string? ModifiedByEmployeeName { get; init; }
    public DateTime? ModifiedDate { get; init; }
}
