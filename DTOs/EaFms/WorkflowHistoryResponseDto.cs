using System;

namespace Jarvis5.Dtos.EaFms;

public class WorkflowHistoryResponseDto
{
    public long Id { get; set; }
    public long WorkflowInstanceId { get; set; }
    public int? FromStatusId { get; set; }
    public string? FromStatusName { get; set; }
    public int? ToStatusId { get; set; }
    public string? ToStatusName { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}
