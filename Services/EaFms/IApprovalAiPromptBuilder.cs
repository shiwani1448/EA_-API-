using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>Builds the system/user prompts for the three Approval AI capabilities
/// (readiness check, approver suggestion, status summary) — the same hardcoded-prompt-
/// builder pattern already used by Meeting/Travel. Contains no Claude/HTTP logic.</summary>
public interface IApprovalAiPromptBuilder
{
    string BuildReadinessSystemPrompt();
    string BuildReadinessUserPrompt(ApprovalAiReadinessInput detail);

    string BuildApproverSystemPrompt();
    string BuildApproverUserPrompt(ApprovalAiApproverInput detail, List<(string Approver, int Count)> candidates);

    string BuildStatusSystemPrompt();
    string BuildStatusUserPrompt(ApprovalDetailDto detail);
}
