using System;

namespace Jarvis5.Dtos.EaFms;

public class MeetingActionDto
{
    public long Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    /// <summary>Opaque stable doer identity, when the record has one. Null for
    /// historical/not-yet-updated rows that only ever captured OwnerName.</summary>
    public string? AssignedToId { get; set; }
    public string? OwnerName { get; set; }
    public string? Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public bool IsOverdue { get; set; }
}
