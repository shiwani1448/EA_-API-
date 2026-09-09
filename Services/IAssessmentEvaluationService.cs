using hrms_api.DTOs;

namespace hrms_api.Services;

public interface IAssessmentEvaluationService
{
    Task<AssessmentEvaluationResponseDto> EvaluateAndSaveAsync(
        AssessmentEvaluateAndSaveRequestDto request,
        IFormFileCollection formFiles,
        CancellationToken cancellationToken = default);

    Task<List<AssessmentEvaluationWithCandidateResponseDto>> GetAllAsync(
        int? candidateId,
        CancellationToken cancellationToken = default);

    Task<AssessmentEvaluationWithCandidateResponseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<AssessmentEvaluationWithCandidateResponseDto?> GetLatestByCandidateIdAsync(
        int candidateId,
        CancellationToken cancellationToken = default);
}
