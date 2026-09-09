using System.Text.Json;

namespace hrms_api.Models;

public class AssessmentEvaluation
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public int? InterviewId { get; set; }
    public int? RoundId { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string RoundName { get; set; } = string.Empty;
    public decimal TotalMarks { get; set; }
    public decimal ObtainedMarks { get; set; }
    public decimal Percentage { get; set; }
    public string Result { get; set; } = "Pending Review";
    public string? Recommendation { get; set; }
    public string? AiSummary { get; set; }
    public string? Strengths { get; set; }
    public string? Weaknesses { get; set; }
    public bool ManualReviewRequired { get; set; }
    public JsonDocument EvaluationJson { get; set; } = JsonDocument.Parse("[]");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public Candidate? Candidate { get; set; }
}
