namespace hrms_api.Services;

public interface ICandidateStatusService
{
    Task ShortlistCandidateAsync(int candidateId);
    Task RejectCandidateAsync(int candidateId);
    Task MarkPendingReviewAsync(int candidateId, string reason);
}
