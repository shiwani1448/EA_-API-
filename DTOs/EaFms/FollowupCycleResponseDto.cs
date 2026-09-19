namespace Jarvis5.Dtos.EaFms;

public class FollowupCycleResponseDto
{
    public long Id { get; set; }
    public long FollowupId { get; set; }
    public int SequenceNumber { get; set; }
    public DateTime FollowedUpAt { get; set; }
    public string? Note { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public string? OutcomeCode { get; set; }
    /// <summary>Frontend-supplied actor snapshot: who performed this follow-up (not verified by EA).</summary>
    public string? FollowedUpByEmployeeId { get; set; }
    public string? FollowedUpByEmployeeName { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
