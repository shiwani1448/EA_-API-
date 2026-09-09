using System.Text;
using Jarvis5.Common;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Entities;

namespace Jarvis5.Services.Ai;

public class AnalysisPromptBuilder : IAnalysisPromptBuilder
{
    public string BuildSystemPrompt() => """
        You are a Senior Business Analyst, Digital Transformation Consultant and Operations
        Excellence Consultant with more than 20 years of enterprise experience.

        Your responsibility is to analyse business automation requests submitted by employees.

        Think like:
        - Director
        - CEO
        - COO
        - Department Head
        - Business Consultant

        Do not think like a software developer.
        Do not generate source code.
        Do not generate APIs.
        Do not generate SQL.

        Your responsibility is to understand business problems.

        Always explain everything in simple business language that non-technical users can understand.
        Always think from a company growth perspective.
        Always think from a business risk perspective.
        Always think about future scalability for the next 3 to 5 years.

        If previous company data is available, reuse existing knowledge instead of creating
        completely new answers.
        If no previous request exists, mention that no similar request was found.

        Always return valid JSON.
        Never return markdown.
        Never return explanations outside JSON.
        """;

    public string BuildUserPrompt(SCIHRequest request, List<SimilarRequestDto> similarRequests)
    {
        var painPoints = JsonHelper.DeserializeList<Dtos.PainPointDto>(request.PainPointsJson);

        var sb = new StringBuilder();

        sb.AppendLine("CURRENT REQUEST");
        sb.AppendLine($"Request Number: {request.RequestNo}");
        sb.AppendLine($"Title: {request.Title}");
        sb.AppendLine($"Department: {request.DepartmentId}");
        sb.AppendLine($"Priority: {request.Priority}");
        sb.AppendLine($"Expected Benefit: {request.ExpectedBenefit}");
        sb.AppendLine("Pain Points:");
        if (painPoints.Count == 0)
        {
            sb.AppendLine("- (none recorded)");
        }
        else
        {
            foreach (var p in painPoints)
                sb.AppendLine($"- {p.Title} (Severity: {p.Severity}, Frequency: {p.Frequency}): {p.Description}");
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("PREVIOUS REQUESTS");
        if (similarRequests.Count == 0)
        {
            sb.AppendLine("No similar request was found.");
        }
        else
        {
            foreach (var s in similarRequests)
            {
                sb.AppendLine($"- {s.RequestNo}: {s.Title} (Department: {s.DepartmentId}, Priority: {s.Priority})");
                sb.AppendLine($"  Expected Benefit: {s.ExpectedBenefit}");
                if (s.PainPointTitles.Count > 0)
                    sb.AppendLine($"  Pain Points: {string.Join("; ", s.PainPointTitles)}");
            }
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("PREVIOUS ANALYSIS");
        var withAnalysis = similarRequests.Where(s => !string.IsNullOrWhiteSpace(s.PreviousAnalysisExecutiveSummary)).ToList();
        if (withAnalysis.Count == 0)
        {
            sb.AppendLine("No previous analysis is available.");
        }
        else
        {
            foreach (var s in withAnalysis)
                sb.AppendLine($"- {s.RequestNo} executive summary: {s.PreviousAnalysisExecutiveSummary}");
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("PREVIOUS APPROVED SOLUTIONS");
        sb.AppendLine("No previous approved solutions are available yet (Solution Design/Approval stages are not yet implemented).");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Now analyse this request. Return ONLY a single JSON object with exactly this shape. " +
            "Critical formatting rules: use standard straight double-quote characters only (never curly/smart quotes); " +
            "put each piece of information directly into its own named field; never write the whole analysis as " +
            "one long paragraph inside a single field (for example, do not put your full analysis inside " +
            "similarity.summary — that field is only a one- or two-sentence note about similarity to past requests); " +
            "fill in every field, use empty arrays/strings only where genuinely not applicable; " +
            "keep executiveSummary under 250 words:");
        sb.AppendLine("""
            {
              "similarity": {
                "similarRequestFound": boolean,
                "similarityPercentage": number,
                "previousRequestNo": string|null,
                "previousRequestTitle": string|null,
                "reusePossible": boolean,
                "reusePercentage": number,
                "estimatedDevelopmentSaving": string,
                "summary": string
              },
              "currentBusinessProcess": { "description": string },
              "bottlenecks": [ { "title": string, "description": string, "affectedUsers": string, "businessImpact": string, "frequency": string, "severity": string, "reason": string } ],
              "rootCauses": [ { "title": string, "description": string, "category": [string] } ],
              "impactPriority": [ { "priority": "Critical|High|Medium|Low", "reason": string } ],
              "directorPerspective": { "businessValue": string, "expectedBenefits": string, "riskReduction": string, "futureBusinessGrowth": string },
              "futureReadiness": { "futureChallenges": [string], "futureDepartments": [string], "futureIntegrations": [string], "automationOpportunities": [string], "aiOpportunities": [string], "reportingSuggestions": [string] },
              "recommendations": { "immediate": [string], "mediumTerm": [string], "longTerm": [string] },
              "executiveSummary": string
            }
            """);

        return sb.ToString();
    }
}
