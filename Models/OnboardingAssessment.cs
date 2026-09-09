using System.Text.Json;

namespace hrms_api.Models;

public class OnboardingAssessment
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public int DurationDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpectedEndDate { get; set; }
    public int CurrentDay { get; set; }
    public string Status { get; set; } = "InProgress";
    public JsonDocument PlanJson { get; set; } = JsonDocument.Parse("{}");
    public JsonDocument ResponsesJson { get; set; } = JsonDocument.Parse("{\"days\":[]}");
    public JsonDocument? FinalEvaluationJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
    public Candidate? Candidate { get; set; }
}
