using System.Text;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public class ApprovalAiPromptBuilder : IApprovalAiPromptBuilder
{
    // ============================================================
    // Readiness check
    // ============================================================

    public string BuildReadinessSystemPrompt() => """
        You help an Executive Assistant judge whether an approval request looks ready to be
        decided by its approver.

        Strict rules:
        - You can see only the request's own field values and the FILE NAMES of documents
          already attached — you have no ability to open or read the contents of any file.
          Never claim to know what a document contains.
        - Only list a field in "missingFields" if its value below is genuinely blank/null —
          never invent a requirement that wasn't stated.
        - "suggestedDocuments" must be phrased as general, typical expectations for the
          stated request type (e.g. "an invoice or quote is typically expected for an
          expense-type request") — never a claim that a specific document is required or
          that a specific one is missing from its contents.
        - If the request is currently in ChangesRequested status, check whether the stated
          change reason still looks unaddressed given the current field values, and mention
          it in "notes" if so.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildReadinessUserPrompt(ApprovalAiReadinessInput detail)
    {
        var sb = new StringBuilder();
        if (detail.SavedRequestId == null)
        {
            sb.AppendLine("UNSAVED FORM: required fields are requestTitle, requestType, department, description, requiredApprovalDate, approverName, amount.");
            sb.AppendLine("Use exactly those camelCase names in missingFields. If all are supplied and the file names look adequate, isLikelyReady should be true.");
        }
        sb.AppendLine("APPROVAL REQUEST DETAILS");
        sb.AppendLine($"Title: {detail.RequestTitle ?? "(not specified)"}");
        sb.AppendLine($"Type: {detail.RequestType ?? "(not specified)"}");
        sb.AppendLine($"Department: {detail.Department ?? "(not specified)"}");
        sb.AppendLine($"Priority: {detail.Priority ?? "(not specified)"}");
        sb.AppendLine($"Description: {detail.Description ?? "(not specified)"}");
        sb.AppendLine($"Justification: {detail.Justification ?? "(not specified)"}");
        sb.AppendLine($"Amount: {detail.Amount?.ToString() ?? "(not specified)"} {detail.Currency}");
        sb.AppendLine($"Required approval date: {detail.RequiredApprovalDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}");
        sb.AppendLine($"Designated approver: {detail.ApproverName ?? "(not specified)"}");
        sb.AppendLine($"Workflow status: {detail.WorkflowStatus ?? "(unknown)"} (cycle {detail.CurrentCycleNo})");
        if (string.Equals(detail.WorkflowStatus, "ChangesRequested", StringComparison.OrdinalIgnoreCase) && detail.LatestCycle?.ChangeReason is { } reason)
            sb.AppendLine($"Change requested reason (cycle {detail.LatestCycle.CycleNo}): {reason}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("ATTACHED DOCUMENTS (file names only — contents not visible)");
        if (detail.DocumentFileNames == null || detail.DocumentFileNames.Count == 0)
            sb.AppendLine("No documents attached.");
        else
            foreach (var name in detail.DocumentFileNames) sb.AppendLine($"- {name}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""
            {
              "isLikelyReady": boolean,
              "missingFields": [string],
              "suggestedDocuments": [string],
              "notes": string
            }
            """);
        return sb.ToString();
    }

    // ============================================================
    // Approver suggestion
    // ============================================================

    public string BuildApproverSystemPrompt() => """
        You help an Executive Assistant pick who should approve a request, based only on who
        approved similar past requests — there is no employee directory or org chart
        available to you.

        Strict rules:
        - You may only recommend a name that appears EXACTLY in the CANDIDATES list below.
          Never invent a new name, title, department, or role.
        - If none of the candidates seem like a good fit, return null rather than forcing a
          choice.
        - Reasoning must be one or two sentences, referencing only the historical counts and
          request details given below.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildApproverUserPrompt(ApprovalAiApproverInput detail, List<(string Approver, int Count)> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CURRENT REQUEST");
        sb.AppendLine($"Department: {detail.Department ?? "(not specified)"}");
        sb.AppendLine($"Type: {detail.RequestType ?? "(not specified)"}");
        sb.AppendLine($"Priority: {detail.SavedPriority ?? "(not specified)"}");
        sb.AppendLine($"Amount: {detail.Amount?.ToString() ?? "(not specified)"} {detail.Currency}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("CANDIDATES (name : number of past approved requests in this department)");
        foreach (var c in candidates) sb.AppendLine($"- {c.Approver} : {c.Count}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "recommendedApprover": string|null, "reasoning": string }""");
        return sb.ToString();
    }

    // ============================================================
    // Status summary
    // ============================================================

    public string BuildStatusSystemPrompt() => """
        You write a short, plain-English status summary of an approval request for a busy
        Executive Assistant, based only on the cycle history and due-state given below.

        Strict rules:
        - Never invent a date, name, or reason that isn't present below.
        - Keep it to one or two sentences.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildStatusUserPrompt(ApprovalDetailDto detail)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Workflow status: {detail.WorkflowStatus ?? "(unknown)"}");
        sb.AppendLine($"Current cycle: {detail.CurrentCycleNo}");
        sb.AppendLine($"Due state: {detail.DueState}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("CYCLE HISTORY (oldest first)");
        foreach (var c in detail.Cycles)
        {
            sb.AppendLine($"- Cycle {c.CycleNo}: status={c.Status ?? "(unknown)"}, submittedAt={c.SubmittedAt?.ToString("yyyy-MM-dd") ?? "(n/a)"}, " +
                $"changeReason={c.ChangeReason ?? "(none)"}, decisionComment={c.DecisionComment ?? "(none)"}");
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "summary": string }""");
        return sb.ToString();
    }
}
