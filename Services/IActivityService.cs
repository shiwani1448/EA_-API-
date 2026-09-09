namespace hrms_api.Services;

public interface IActivityService
{
    Task AddAiScreeningActivityAsync(int candidateId, string decision, decimal? score, string? remarks);
}
