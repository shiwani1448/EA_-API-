using hrms_api.DTOs;

namespace hrms_api.Services;

public interface IDirectorRoundInsightService
{
    Task<(int StatusCode, DirectorRoundCandidateInsightResponseDto Response)> GetCandidateInsightAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken = default);
}
