using System.Text.Json;

namespace hrms_api.Models;

public class CandidateActivity
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? TotalScore { get; set; }
    public JsonDocument? EvaluationJson { get; set; }
    public string? Remarks { get; set; }
    public DateTime ActionDate { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Candidate? Candidate { get; set; }
}
