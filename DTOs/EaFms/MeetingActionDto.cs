using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingActionDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    /// <summary>Opaque stable doer identity, when the record has one. Null for
    /// historical/not-yet-updated rows that only ever captured DoerName.</summary>
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }
    public string? Priority { get; set; }
    public DateTime? DueDate { get; set; }
    /// <summary>Planned start time; becomes the Delegation's startDate on Meeting completion.</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>Assignee (a separate person from the Doer); becomes the Delegation's assignee.</summary>
    public string? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    /// <summary>Becomes the Delegation's delegationType (drives its TAT rule).</summary>
    public string? DelegationType { get; set; }
    public string? Status { get; set; }
    public bool IsOverdue { get; set; }
}
