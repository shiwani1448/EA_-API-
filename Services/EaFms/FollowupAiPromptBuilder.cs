using System.Text;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public class FollowupAiPromptBuilder : IFollowupAiPromptBuilder
{
    // ============================================================
    // Reminder draft
    // ============================================================

    public string BuildReminderSystemPrompt() => """
        You draft a short, polite follow-up reminder email for an Executive Assistant to send
        on a pending item.

        Strict rules:
        - Use only the facts given below — never invent a deadline, name, or detail that isn't
          stated.
        - Keep the body to 3-5 short sentences: what is pending, since when/how overdue it is
          (only if the data below says so), and a polite ask for an update.
        - The subject must be short and specific (reference the actual subject/type below, not
          a generic "Reminder").
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildReminderUserPrompt(FollowupResponseDto followup)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Subject: {followup.Subject ?? "(not specified)"}");
        sb.AppendLine($"Type: {followup.Type ?? "(not specified)"}");
        sb.AppendLine($"Note: {followup.Note ?? "(none)"}");
        sb.AppendLine($"Due at: {followup.DueAt:yyyy-MM-dd}");
        sb.AppendLine($"Overdue: {(followup.IsOverdue ? "yes" : "no")}");
        sb.AppendLine($"Waiting on: {followup.WaitingOnName ?? followup.WaitingOnExternal ?? "(not specified)"}");
        sb.AppendLine($"Doer: {followup.DoerName ?? "(not specified)"}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "subject": string, "body": string, "reasoning": string }""");
        return sb.ToString();
    }

    // ============================================================
    // Escalation suggestion
    // ============================================================

    public string BuildEscalationSystemPrompt() => """
        You help an Executive Assistant decide whether a stalled follow-up should be escalated,
        and to which level, based only on the real facts given below.

        Strict rules:
        - You may only recommend a level whose CODE appears EXACTLY in the CANDIDATE LEVELS
          list below. Never invent a level or name.
        - If the facts don't clearly warrant escalating further right now, return null rather
          than forcing a choice.
        - If a CURRENT ESCALATION LEVEL is given, you may only recommend a level with a higher
          numeric Level than it — never the same or a lower one.
        - Reasoning must be one or two sentences, referencing only the facts given below.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildEscalationUserPrompt(FollowupResponseDto followup, List<(int Id, string Code, string Name, int Level)> candidateLevels, int cycleCount, int? currentEscalationLevel)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Subject: {followup.Subject ?? "(not specified)"}");
        sb.AppendLine($"Due at: {followup.DueAt:yyyy-MM-dd}");
        sb.AppendLine($"Overdue: {(followup.IsOverdue ? "yes" : "no")}");
        sb.AppendLine($"Follow-up attempts recorded so far: {cycleCount}");
        sb.AppendLine($"Current escalation level (numeric, null if never escalated): {(currentEscalationLevel?.ToString() ?? "none")}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("CANDIDATE LEVELS (code : name : numeric level)");
        foreach (var l in candidateLevels) sb.AppendLine($"- {l.Code} : {l.Name} : {l.Level}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "recommendedLevelCode": string|null, "reasoning": string }""");
        return sb.ToString();
    }

    // ============================================================
    // Resolution-time explanation — narrates an ALREADY-COMPUTED date
    // ============================================================

    public string BuildResolutionExplanationSystemPrompt() => """
        You explain, in one short sentence, why a follow-up's predicted resolution date is
        what it is, based only on the basis and date already given below.

        Strict rules:
        - Never invent a different date or basis than the ones given.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildResolutionExplanationUserPrompt(FollowupResponseDto followup, string basis, DateTime suggested)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Subject: {followup.Subject ?? "(not specified)"}");
        sb.AppendLine($"Type: {followup.Type ?? "(not specified)"}");
        sb.AppendLine($"Basis: {basis}");
        sb.AppendLine($"Already-computed predicted resolution date: {suggested:yyyy-MM-dd}");
        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "explanation": string }""");
        return sb.ToString();
    }

    // ============================================================
    // At-risk check
    // ============================================================

    public string BuildAtRiskSystemPrompt() => """
        You judge how at-risk a follow-up is of never getting resolved, based only on the real
        facts given below.

        Strict rules:
        - riskLevel must be exactly one of: Low, Medium, High.
        - Base the judgement only on the facts given (how overdue, how many follow-up attempts
          already made without resolution, whether it is already escalated and unresolved) —
          never invent additional facts.
        - suggestedAction must be one short, concrete next step (e.g. "escalate to the next
          level", "send a reminder today") — never vague.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildAtRiskUserPrompt(FollowupResponseDto followup, int cycleCount, int? currentEscalationLevel, bool hasUnresolvedEscalation)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Subject: {followup.Subject ?? "(not specified)"}");
        sb.AppendLine($"Due at: {followup.DueAt:yyyy-MM-dd}");
        sb.AppendLine($"Overdue: {(followup.IsOverdue ? "yes" : "no")}");
        sb.AppendLine($"Follow-up attempts recorded so far: {cycleCount}");
        sb.AppendLine($"Current escalation level (numeric, null if never escalated): {(currentEscalationLevel?.ToString() ?? "none")}");
        sb.AppendLine($"Has an unresolved escalation right now: {(hasUnresolvedEscalation ? "yes" : "no")}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "riskLevel": string, "reasoning": string, "suggestedAction": string }""");
        return sb.ToString();
    }
}
