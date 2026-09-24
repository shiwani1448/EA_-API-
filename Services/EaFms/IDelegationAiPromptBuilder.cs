using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>Builds the system/user prompts for the three Delegation AI capabilities
/// (owner suggestion, due-date prediction, delay-risk check) — the same hardcoded-prompt-
/// builder pattern already used by Meeting/Travel/Approval. Contains no Claude/HTTP logic.</summary>
public interface IDelegationAiPromptBuilder
{
    string BuildOwnerSystemPrompt();
    string BuildOwnerUserPrompt(DelegationResponseDto delegation, List<(string DoerId, string? DoerName, int Count)> candidates);

    string BuildDueDateSystemPrompt();
    string BuildDueDateUserPrompt(DelegationResponseDto delegation, string basis, DateTime suggestedDate);

    string BuildDelayRiskSystemPrompt();
    string BuildDelayRiskUserPrompt(DelegationResponseDto delegation);
}
