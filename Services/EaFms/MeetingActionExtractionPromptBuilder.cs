using System.Text;

namespace Jarvis5.Services.EaFms;

/// <summary>Builds the system/user prompts for Meeting completion-evidence action-point
/// extraction — the same hardcoded-prompt-builder pattern already used by SCIH's
/// AnalysisPromptBuilder/SolutionDesignPromptBuilder. Contains no Claude/HTTP logic.</summary>
public class MeetingActionExtractionPromptBuilder : IMeetingActionExtractionPromptBuilder
{
    public string BuildSystemPrompt() => """
        You are an assistant that reads meeting completion evidence (minutes of meeting
        text and/or an extracted meeting completion PDF) for an Executive Assistant (EA)
        task-management system.

        Your ONLY job is to identify concrete, actionable action points that were assigned
        or agreed upon during the meeting, so an EA can review them before turning any of
        them into real tasks.

        Strict rules:
        - Extract only clearly actionable tasks — something a named or described person is
          expected to DO. Do not turn general discussion, background context, opinions, or
          status updates into action points.
        - Never invent a person's name. Only set a doer name when the source text clearly
          attributes the action to a specific person. Otherwise leave it null.
        - Never invent a due date. Only set a due date when the source text states one
          (explicitly, or as an unambiguous concrete date). Otherwise leave it null.
        - Never invent a priority. Only set a priority when the source text states or
          strongly implies one. Otherwise leave it null.
        - You are not making a business decision and you must not decide who will actually
          own the task — you are only proposing candidates for a human EA to review.
        - Use null for any field the source does not clearly support. Do not guess or fill
          in plausible-sounding values merely to complete the shape.
        - If no actionable items are found at all, return an empty proposedActions array.

        Output rules:
        - Return ONLY a single valid JSON object.
        - Never return markdown, code fences, or any text outside the JSON object.
        - Never add explanatory prose before or after the JSON.
        - Use standard straight double-quote characters only.
        """;

    public string BuildUserPrompt(string? mom, string? pdfText)
    {
        var sb = new StringBuilder();

        sb.AppendLine("MEETING COMPLETION EVIDENCE");
        sb.AppendLine("------------------------------------------------");

        if (!string.IsNullOrWhiteSpace(mom))
        {
            sb.AppendLine("MINUTES OF MEETING (entered by the EA):");
            sb.AppendLine(mom);
            sb.AppendLine("------------------------------------------------");
        }

        if (!string.IsNullOrWhiteSpace(pdfText))
        {
            sb.AppendLine("TEXT EXTRACTED FROM THE UPLOADED COMPLETION PDF:");
            sb.AppendLine(pdfText);
            sb.AppendLine("------------------------------------------------");
        }

        sb.AppendLine("Read the evidence above and extract every clearly actionable item. " +
            "Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""
            {
              "proposedActions": [
                {
                  "title": string,
                  "description": string|null,
                  "doerName": string|null,
                  "priority": string|null,
                  "dueDate": string|null
                }
              ]
            }
            """);
        sb.AppendLine("\"title\" is a short summary of the action and is the only field that must " +
            "always be filled in for an item to be included at all. \"dueDate\", when present, must be " +
            "an ISO 8601 date (YYYY-MM-DD). If there are no actionable items, return " +
            "{ \"proposedActions\": [] }.");

        return sb.ToString();
    }
}
