using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace hrms_api.DTOs;

public sealed class StartOnboardingAssessmentDto
{
    [Range(1, 30)] public int DurationDays { get; set; }
    [Range(1, 12)] public decimal DailyWorkingHours { get; set; } = 8;
}

public sealed class OnboardingSubmissionFileDto
{
    [Required] public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? Url { get; set; }
}

public sealed class OnboardingTaskSubmissionDto
{
    [Required] public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = "SUBMITTED";
    public string? TextResponse { get; set; }
    public List<OnboardingSubmissionFileDto> Files { get; set; } = new();
}

public sealed class DailyOnboardingSubmissionDto
{
    public List<OnboardingTaskSubmissionDto> Tasks { get; set; } = new();
}

public sealed class SaveOnboardingTaskDto
{
    public string Status { get; set; } = "IN_PROGRESS";
    public string? TextResponse { get; set; }
    public List<OnboardingSubmissionFileDto> Files { get; set; } = new();
}

public sealed class OnboardingHrDecisionDto
{
    [Required] public string Decision { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}

public sealed class OnboardingAssessmentResponseDto
{
    public int AssessmentId { get; set; }
    public int CandidateId { get; set; }
    public int DurationDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpectedEndDate { get; set; }
    public int CurrentDay { get; set; }
    public string Status { get; set; } = string.Empty;
    public JsonElement Plan { get; set; }
    public JsonElement Responses { get; set; }
    public JsonElement? FinalEvaluation { get; set; }
}

public sealed class CurrentOnboardingDayDto
{
    public int AssessmentId { get; set; }
    public int Day { get; set; }
    public string? Title { get; set; }
    public string? Objective { get; set; }
    public JsonElement Tasks { get; set; }
    public JsonElement SavedTasks { get; set; }
    public string SubmissionStatus { get; set; } = "Pending";
}
