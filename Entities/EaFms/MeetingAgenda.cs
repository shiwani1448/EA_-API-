using System;

namespace Jarvis5.Entities.EaFms;

public class MeetingAgenda
{
    public long Id { get; set; }
    public long MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public int SequenceNumber { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }

    public string? OwnerId { get; set; }
    public string? OwnerName { get; set; }

    public bool IsPrepared { get; set; }
    public DateTime? PreparedAt { get; set; }
    public string? PreparedById { get; set; }
    public string? PreparedByName { get; set; }

    public DateTime? RequiredBy { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
