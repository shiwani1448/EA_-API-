using System;

namespace Jarvis5.Entities.EaFms;

public class MeetingAction
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public string? ActionRecordId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? OwnerName { get; set; }
    public int? PriorityLevelId { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
