using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Preview-only AI assistance for Follow-up &amp; Escalation (reminder draft, escalation
/// suggestion, resolution-time prediction, at-risk check). Orchestration only —
/// provider-specific logic (Claude, JSON parsing) lives in the already-registered shared
/// services; this class never talks to Claude directly. Never writes to Followup/Escalation
/// except through the two explicit apply/send endpoints, which write exactly what the EA
/// confirmed — never a value re-derived from Claude.
/// </summary>
public interface IFollowupAiService
{
    Task<FollowupAiReminderSuggestionResponseDto> SuggestReminderAsync(long followupId, CancellationToken ct = default);
    Task<FollowupAiReminderSentResponseDto> SendSuggestedReminderAsync(long followupId, SendFollowupReminderRequestDto dto, CancellationToken ct = default);

    Task<FollowupAiEscalationSuggestionResponseDto> SuggestEscalationAsync(long followupId, CancellationToken ct = default);
    Task<EscalationResponseDto> ApplySuggestedEscalationAsync(long followupId, ApplySuggestedEscalationRequestDto dto, CancellationToken ct = default);

    Task<FollowupAiResolutionPredictionResponseDto> PredictResolutionTimeAsync(long followupId, CancellationToken ct = default);

    Task<FollowupAiAtRiskResponseDto> CheckAtRiskAsync(long followupId, CancellationToken ct = default);
}
