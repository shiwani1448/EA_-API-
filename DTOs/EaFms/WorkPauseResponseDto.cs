using System;

namespace Jarvis5.Dtos.EaFms;

public class WorkPauseResponseDto
{
    public long Id { get; set; }
    public long? IntakeRequestId { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public long? FollowupId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? Reason { get; set; }
    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }
    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public DateTime? RequestSentAt { get; set; }
    public string? ResumedById { get; set; }
    public string? ResumedByName { get; set; }
    public string? ResumedReason { get; set; }
    public int? WorkflowStatusId { get; set; }
    public string? WorkflowStatusName { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
