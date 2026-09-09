namespace Jarvis5.Dtos.DevelopmentPlan;

public class TaskHistoryDto
{
    public long Id { get; set; }
    public long TaskId { get; set; }
    public long RequestId { get; set; }
    public long? StageId { get; set; }
    public string? StageName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
    public object? PreviousValue { get; set; }
    public object? NewValue { get; set; }
    public string? Remarks { get; set; }
    public long ActionBy { get; set; }
    public DateTime ActionDate { get; set; }
    public string? IPAddress { get; set; }
    public string? DeviceInfo { get; set; }
}
