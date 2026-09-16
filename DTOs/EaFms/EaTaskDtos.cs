using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateEaTaskDto
{
    public long ModuleId { get; set; }
    public string BusinessRecordId { get; set; } = null!;
    public string Task { get; set; } = null!;
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public long? WorkflowInstanceId { get; set; }
}

public class EaTaskResponseDto
{
    public long EaTaskId { get; set; }
    public long ModuleId { get; set; }
    public string ModuleName { get; set; } = null!;
    public string BusinessRecordId { get; set; } = null!;
    public string Task { get; set; } = null!;
    public string? Description { get; set; }
    public int? AllottedTatMinutes { get; set; }
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
