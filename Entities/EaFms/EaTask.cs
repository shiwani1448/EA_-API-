namespace Jarvis5.Entities.EaFms;

public class EaTask
{
    public long Id { get; set; }
    public long BusinessModuleId { get; set; }
    public BusinessModule BusinessModule { get; set; } = null!;
    public string BusinessRecordId { get; set; } = null!;
    public string Task { get; set; } = null!;
    public string? Description { get; set; }
    // Snapshot at creation; module configuration changes never update this value.
    public int? AllottedTatMinutes { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
