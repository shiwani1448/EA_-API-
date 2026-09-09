using System;

namespace Jarvis5.Dtos.EaFms;

public class FollowupResponseDto
{
    public long Id { get; set; }
    public long? IntakeRequestId { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public long? BusinessModuleId { get; set; }
    public string? BusinessModuleCode { get; set; }
    public string? BusinessModuleName { get; set; }
    public string? BusinessRecordId { get; set; }
    public string? BusinessRecordTitle { get; set; }
    public string? PriorityLevelName { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public int? PriorityLevelId { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime? LastFollowupAt { get; set; }
    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }
    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public int? SequenceNumber { get; set; }
    public string? CompletionNote { get; set; }
    public string? OutcomeCode { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public string? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
