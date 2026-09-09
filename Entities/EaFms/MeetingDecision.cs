using System;

namespace Jarvis5.Entities.EaFms;

public class MeetingDecision
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public string? Decision { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? DecisionDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Status { get; set; }
    public string? DecisionRecordId { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
