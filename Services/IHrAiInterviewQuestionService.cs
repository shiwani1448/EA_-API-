using hrms_api.DTOs;

namespace hrms_api.Services;

public interface IHrAiInterviewQuestionService
{
    Task<(int StatusCode, HrAiInterviewQuestionsResponseDto Response)> GenerateAsync(
        GenerateHrAiInterviewQuestionsRequestDto request,
        CancellationToken cancellationToken = default);

    Task<(int StatusCode, HrAiInterviewQuestionsResponseDto Response)> GetAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken = default);
}
