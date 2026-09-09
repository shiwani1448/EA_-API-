using System.Text.Json;

namespace hrms_api.Services;

public interface IOnboardingAiService
{
    Task<JsonDocument> GeneratePlanAsync(object input, int durationDays, CancellationToken cancellationToken = default);
    Task<JsonDocument> EvaluateFinalAsync(object input, int durationDays, CancellationToken cancellationToken = default);
}
