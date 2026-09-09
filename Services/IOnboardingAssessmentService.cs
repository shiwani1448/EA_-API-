using hrms_api.DTOs;

namespace hrms_api.Services;

public interface IOnboardingAssessmentService
{
    Task<OnboardingAssessmentResponseDto> StartAsync(int candidateId, int durationDays, decimal dailyWorkingHours, int? userId, CancellationToken ct);
    Task<OnboardingAssessmentResponseDto> GetAsync(int candidateId, CancellationToken ct);
    Task<CurrentOnboardingDayDto> GetCurrentDayAsync(int candidateId, CancellationToken ct);
    Task<OnboardingAssessmentResponseDto> SubmitDayAsync(int candidateId, int day, DailyOnboardingSubmissionDto request, int? userId, CancellationToken ct);
    Task<OnboardingAssessmentResponseDto> EvaluateAsync(int candidateId, int? userId, CancellationToken ct);
    Task<OnboardingAssessmentResponseDto> DecideAsync(int candidateId, OnboardingHrDecisionDto request, int? userId, CancellationToken ct);
    Task<OnboardingSubmissionFileDto> UploadTaskFileAsync(int candidateId, string taskId, IFormFile file, CancellationToken ct);
    Task<OnboardingAssessmentResponseDto> SaveTaskAsync(int candidateId, int day, string taskId, SaveOnboardingTaskDto request, int? userId, CancellationToken ct);
}
