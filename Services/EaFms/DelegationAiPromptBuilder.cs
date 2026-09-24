using System.Text;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public class DelegationAiPromptBuilder : IDelegationAiPromptBuilder
{
    // ============================================================
    // Suggest owner
    // ============================================================

    public string BuildOwnerSystemPrompt() => """
        You help an Executive Assistant pick who should do a piece of delegated work, based
        only on who actually did similar past delegations — there is no employee directory
        or org chart available to you.

        Strict rules:
        - You may only recommend a doerId that appears EXACTLY in the CANDIDATES list below.
          Never invent a new person, id, or role.
        - If none of the candidates seem like a good fit, return null rather than forcing a
          choice.
        - Reasoning must be one or two sentences, referencing only the historical counts and
          delegation details given below.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildOwnerUserPrompt(DelegationResponseDto delegation, List<(string DoerId, string? DoerName, int Count)> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CURRENT DELEGATION");
        sb.AppendLine($"Title: {delegation.Title}");
        sb.AppendLine($"Description: {delegation.Description ?? "(not specified)"}");
        sb.AppendLine($"Delegation type: {delegation.DelegationType ?? "(not specified)"}");
        sb.AppendLine($"Priority: {delegation.Priority ?? "(not specified)"}");
        sb.AppendLine($"Source module: {delegation.SourceModuleName ?? "(direct/manual delegation)"}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("CANDIDATES (doerId : display name : number of past delegations of this same type)");
        foreach (var c in candidates) sb.AppendLine($"- {c.DoerId} : {c.DoerName ?? "(no name on record)"} : {c.Count}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "recommendedDoerId": string|null, "reasoning": string }""");
        return sb.ToString();
    }

    // ============================================================
    // Predict due date — Claude only explains a date already computed in code
    // ============================================================

    public string BuildDueDateSystemPrompt() => """
        You explain, in one or two plain-English sentences, why a due date was suggested for
        a piece of delegated work. The date itself has ALREADY been calculated from real data
        — you are not calculating or changing it, only explaining the reasoning behind the
        number you are given.

        Strict rules:
        - Never state a different date than the one given to you.
        - Reference only the basis given (a configured turnaround time, or the average of
          past similar delegations) — never invent a reason not stated below.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildDueDateUserPrompt(DelegationResponseDto delegation, string basis, DateTime suggestedDate)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Delegation type: {delegation.DelegationType ?? "(not specified)"}");
        sb.AppendLine($"Title: {delegation.Title}");
        sb.AppendLine($"Basis for the suggestion: {(basis == "ConfiguredTat" ? "a configured turnaround-time rule for this delegation type" : "the average completion time of past completed delegations of this same type")}");
        sb.AppendLine($"Already-computed suggested due date: {suggestedDate:yyyy-MM-dd HH:mm}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "explanation": string }""");
        return sb.ToString();
    }

    // ============================================================
    // Delay risk check
    // ============================================================

    public string BuildDelayRiskSystemPrompt() => """
        You assess how likely a piece of delegated work is to be late, using only the real
        execution data given below — never invent a fact not present.

        Strict rules:
        - riskLevel must be exactly one of: Low, Medium, High.
        - Reasoning must be one or two sentences, referencing only the data given.
        - suggestedNudgeMessage is a short, friendly reminder message the EA could copy and
          send to the doer — return null when risk is Low and no nudge is warranted. This
          message is only ever a draft for the EA to send manually; you are not sending
          anything.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildDelayRiskUserPrompt(DelegationResponseDto delegation)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Title: {delegation.Title}");
        sb.AppendLine($"Doer: {delegation.DoerName ?? delegation.DoerId}");
        sb.AppendLine($"Status: {delegation.Status}");
        sb.AppendLine($"Planned start date: {delegation.StartDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}");
        sb.AppendLine($"Planned due date: {delegation.EndDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}");
        sb.AppendLine($"Is due today: {delegation.IsDueToday}");
        sb.AppendLine($"Is overdue: {delegation.IsOverdue}");
        sb.AppendLine($"Actually started at: {delegation.StartedAt?.ToString("yyyy-MM-dd HH:mm") ?? "(not started yet)"}");
        sb.AppendLine($"Allotted turnaround minutes: {delegation.AllottedTatMinutes?.ToString() ?? "(no TAT configured)"}");
        sb.AppendLine($"Turnaround minutes used so far: {delegation.TatUsedMinutes?.ToString() ?? "(not available)"}");
        sb.AppendLine($"Turnaround minutes paused so far: {delegation.TatPausedMinutes?.ToString() ?? "(not available)"}");
        sb.AppendLine($"Number of times paused: {delegation.TatSummary.PauseCount}");
        sb.AppendLine($"Currently paused: {delegation.IsPaused}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""
            {
              "riskLevel": "Low|Medium|High",
              "reasoning": string,
              "suggestedNudgeMessage": string|null
            }
            """);
        return sb.ToString();
    }
}
