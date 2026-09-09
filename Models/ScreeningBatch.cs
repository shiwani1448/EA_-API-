namespace hrms_api.Models;

public class ScreeningBatch
{
    public string BatchId { get; set; } = Guid.NewGuid().ToString("N");
    public string Status { get; set; } = "Queued";
    public int TotalCandidates { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int PendingReview { get; set; }
    public int Shortlisted { get; set; }
    public int Rejected { get; set; }
    public string? FailureReason { get; set; }
    public bool ForceRescreen { get; set; }
    public string? CandidateIdsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
