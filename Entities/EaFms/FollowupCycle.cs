namespace Jarvis5.Entities.EaFms;

public class FollowupCycle
{
    public long Id { get; set; }
    public long FollowupId { get; set; }
    public Followup? Followup { get; set; }
    public int SequenceNumber { get; set; }
    public DateTime FollowedUpAt { get; set; }
    public string? Note { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public string? OutcomeCode { get; set; }
    // Frontend-supplied actor snapshot of who performed this follow-up (not verified by EA).
    public string? FollowedUpByEmployeeId { get; set; }
    public string? FollowedUpByEmployeeName { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
