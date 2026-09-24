using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IFollowupAiPromptBuilder
{
    string BuildReminderSystemPrompt();
    string BuildReminderUserPrompt(FollowupResponseDto followup);

    string BuildEscalationSystemPrompt();
    string BuildEscalationUserPrompt(FollowupResponseDto followup, List<(int Id, string Code, string Name, int Level)> candidateLevels, int cycleCount, int? currentEscalationLevel);

    string BuildResolutionExplanationSystemPrompt();
    string BuildResolutionExplanationUserPrompt(FollowupResponseDto followup, string basis, DateTime suggested);

    string BuildAtRiskSystemPrompt();
    string BuildAtRiskUserPrompt(FollowupResponseDto followup, int cycleCount, int? currentEscalationLevel, bool hasUnresolvedEscalation);
}
