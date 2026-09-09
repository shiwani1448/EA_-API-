using hrms_api.DTOs;

namespace hrms_api.Services;

public interface IDirectorAiInterviewQuestionService
{
    Task<(int StatusCode, DirectorAiInterviewQuestionsResponseDto Response)> GenerateAsync(
        GenerateDirectorAiInterviewQuestionsRequestDto request,
        CancellationToken cancellationToken = default);

    Task<(int StatusCode, DirectorAiInterviewQuestionsResponseDto Response)> GetAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken = default);
}
