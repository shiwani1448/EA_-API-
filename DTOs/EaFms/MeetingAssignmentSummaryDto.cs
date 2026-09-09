using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingAssignmentSummaryDto
{
    public string? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public string? AssignedById { get; set; }
    public string? AssignedByName { get; set; }
    public DateTime? AssignedAt { get; set; }
    public string? AssignmentType { get; set; }
    public string? AssignmentReason { get; set; }
    public string? PreviousAssignedToId { get; set; }
    public string? PreviousAssignedToName { get; set; }
    public bool IsAssigned { get; set; }
    public bool IsCurrent { get; set; }
}
