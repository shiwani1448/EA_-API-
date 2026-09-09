using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace hrms_api.DTOs;

public class AssessmentEvaluateAndSaveRequestDto
{
    [FromForm(Name = "candidateId")]
    public int CandidateId { get; set; }

    [FromForm(Name = "interviewId")]
    public int? InterviewId { get; set; }

    [FromForm(Name = "roundId")]
    public int? RoundId { get; set; }

    [FromForm(Name = "department")]
    public string? Department { get; set; }

    [FromForm(Name = "designation")]
    public string? Designation { get; set; }

    [FromForm(Name = "level")]
    public string? Level { get; set; }

    [FromForm(Name = "roundName")]
    public string? RoundName { get; set; }

    [FromForm(Name = "createdBy")]
    public string? CreatedBy { get; set; }

    [FromForm(Name = "questions")]
    public string? QuestionsJson { get; set; }

    [FromForm(Name = "files")]
    public List<IFormFile> Files { get; set; } = new();
}

public class AssessmentEvaluationQuestionInputDto
{
    public int QuestionId { get; set; }
    public string? QuestionText { get; set; }
    public string? QuestionType { get; set; }
    public string? Category { get; set; }
    public string? Difficulty { get; set; }
    public string? AnswerText { get; set; }
    public string? Remark { get; set; }
    public decimal MaxMarks { get; set; }
    public string? FileKey { get; set; }
    public string? FileFieldName { get; set; }
}

public class AssessmentEvaluationResponseDto
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
    public string Result { get; set; } = string.Empty;
    public string? Recommendation { get; set; }
    public string? AiSummary { get; set; }
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
    public bool ManualReviewRequired { get; set; }
    public JsonDocument EvaluationJson { get; set; } = JsonDocument.Parse("[]");
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class AssessmentEvaluationCandidateDto
{
    public int CandidateId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? KeySkills { get; set; }
    public string? Source { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
}

public class AssessmentEvaluationWithCandidateResponseDto : AssessmentEvaluationResponseDto
{
    public AssessmentEvaluationCandidateDto? Candidate { get; set; }
}
