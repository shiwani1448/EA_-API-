using hrms_api.DTOs;

namespace hrms_api.Services;

public interface IAiScreeningService
{
    Task<AiScreeningBatchResponseDto> RunBatchAsync(AiScreeningBatchRequestDto request);
    Task<AiScreeningResultItemDto> ScreenCandidateAsync(int candidateId, bool forceRescreen);
    Task<AiScreeningResultItemDto> ScreenCandidateAsync(
        int candidateId,
        bool forceRescreen,
        string? batchId,
        CancellationToken cancellationToken = default);
}
