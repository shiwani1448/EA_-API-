using System;

namespace Jarvis5.Entities.EaFms;

public class MeetingMinutes
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public string? Summary { get; set; }
    public string? DiscussionNotes { get; set; }

    public string? PreparedById { get; set; }
    public string? PreparedByName { get; set; }
    public DateTime? PreparedAt { get; set; }

    public string? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public string? Status { get; set; }
    public int RevisionNumber { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
