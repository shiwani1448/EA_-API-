using System;
using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateFollowupRequestDto
{
    public long? IntakeRequestId { get; set; }
    public long? WorkflowInstanceId { get; set; }
    public DateTime DueAt { get; set; }
    public string? Note { get; set; }
    public string? Subject { get; set; }
    public string? Type { get; set; }
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public int? PriorityLevelId { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public string? ResponseOwnerId { get; set; }
    public string? ResponseOwnerName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public string? WaitingOnId { get; set; }
    public string? WaitingOnName { get; set; }
    public string? WaitingOnExternal { get; set; }
    public int? SequenceNumber { get; set; }

    // Optional source linkage; both fields may be null for standalone follow-ups.
    public long? BusinessModuleId { get; set; }
    public string? BusinessRecordId { get; set; }
}
