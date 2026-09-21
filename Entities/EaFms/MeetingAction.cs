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
    // Opaque stable doer identity, supplied by the caller when available. Nullable:
    // EmployeeLookup does not exist yet, so historical and not-yet-updated rows only
    // ever have DoerName. Never derived/backfilled from DoerName — display text is
    // not an identity. Future Meeting -> Delegation mapping: DoerId -> Delegation.
    // DoerId, DoerName -> Delegation.DoerNameSnapshot.
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }
    // Frontend-owned business string, stored as submitted — not restricted to
    // PriorityLevel's catalog (PriorityLevel is optional discovery data only).
    public string? Priority { get; set; }
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
