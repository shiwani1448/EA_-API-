using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingAssignmentSummaryDto
{
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }
    public string? AssignedById { get; set; }
    public string? AssignedByName { get; set; }
    public DateTime? AssignedAt { get; set; }
    public string? AssignmentType { get; set; }
    public string? AssignmentReason { get; set; }
    public string? PreviousDoerId { get; set; }
    public string? PreviousDoerName { get; set; }
    public bool IsAssigned { get; set; }
    public bool IsCurrent { get; set; }
}
