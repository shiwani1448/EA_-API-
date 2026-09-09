using System.Text;
using Jarvis5.Common;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;

namespace Jarvis5.Services.Ai;

public class AnalysisReworkPromptBuilder : IAnalysisReworkPromptBuilder
{
    public string BuildSystemPrompt() => """
        You are a Senior Business Consultant.

        A previous analysis has already been completed.

        The Director reviewed it and requested improvements.

        Do NOT create a completely new analysis.

        Instead:

        1. Review the previous analysis.

        2. Understand the Director's comments.

        3. Identify weaknesses in the previous analysis.

        4. Improve every weak section.

        5. Preserve useful content.

        6. Add missing information.

        7. Produce a significantly better analysis.

        Never repeat the previous response.

        Every rework must improve quality.

        If previous mistakes still exist,

        correct them.

        Always think from

        Director

        Operations

        Finance

        Scalability

        Business Growth

        Risk Management

        Future Readiness

        Return JSON only.
        """;

    public string BuildUserPrompt(
        SCIHRequest request,
        AiAnalysisResultDto latestAnalysis,
        int latestAnalysisVersion,
        AiAnalysisResultDto? previousAnalysis,
        SolutionDesignResultDto? previousSolutionDesign,
        SCIHApproval latestApproval,
        List<SimilarRequestDto> similarRequests,
        List<PreviousSolutionDesignDto> previousApprovedSolutions)
    {
        var painPoints = JsonHelper.DeserializeList<Dtos.PainPointDto>(request.PainPointsJson);
        var improvementAreas = JsonHelper.DeserializeList<string>(latestApproval.ImprovementAreasJson);

        var sb = new StringBuilder();

        sb.AppendLine("ORIGINAL REQUEST / LATEST REQUEST DETAILS");
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
        sb.AppendLine($"LATEST ANALYSIS (Version {latestAnalysisVersion} — the one being reworked)");
        AppendAnalysis(sb, latestAnalysis);

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("PREVIOUS ANALYSIS (the version before the one above)");
        if (previousAnalysis is null)
        {
            sb.AppendLine("This is the first rework cycle — there is no earlier version than the one above.");
        }
        else
        {
            AppendAnalysis(sb, previousAnalysis);
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("PREVIOUS SOLUTION DESIGN");
        if (previousSolutionDesign is null)
        {
            sb.AppendLine("No solution design is available.");
        }
        else
        {
            sb.AppendLine($"Title: {previousSolutionDesign.SolutionOverview.Title}");
            sb.AppendLine($"Description: {previousSolutionDesign.SolutionOverview.Description}");
            sb.AppendLine($"Executive Summary: {previousSolutionDesign.ExecutiveSummary}");
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("DIRECTOR'S REJECTION COMMENTS");
        sb.AppendLine(string.IsNullOrWhiteSpace(latestApproval.Comments) ? "(none)" : latestApproval.Comments);
        sb.AppendLine($"Rejection Reason: {latestApproval.RejectionReason}");
        sb.AppendLine("Improvement Areas:");
        if (improvementAreas.Count == 0)
        {
            sb.AppendLine("- (none specified)");
        }
        else
        {
            foreach (var area in improvementAreas)
                sb.AppendLine($"- {area}");
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine($"PREVIOUS REWORK COUNT: {latestApproval.ReworkCount}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("PREVIOUS SIMILAR REQUESTS");
        if (similarRequests.Count == 0)
        {
            sb.AppendLine("No similar request was found.");
        }
        else
        {
            foreach (var s in similarRequests)
            {
                sb.AppendLine($"- {s.RequestNo}: {s.Title} (Department: {s.DepartmentId}, Priority: {s.Priority})");
                if (!string.IsNullOrWhiteSpace(s.PreviousAnalysisExecutiveSummary))
                    sb.AppendLine($"  Executive Summary: {s.PreviousAnalysisExecutiveSummary}");
            }
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("PREVIOUS APPROVED SOLUTIONS");
        if (previousApprovedSolutions.Count == 0)
        {
            sb.AppendLine("No previous approved solutions are available.");
        }
        else
        {
            foreach (var s in previousApprovedSolutions)
            {
                sb.AppendLine($"- {s.RequestNo}: {s.SolutionTitle} — {s.SolutionDescription} (Status: {s.Status})");
                if (s.ModuleNames.Count > 0)
                    sb.AppendLine($"  Modules: {string.Join("; ", s.ModuleNames)}");
            }
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Now produce the improved analysis. Return ONLY a single JSON object with exactly " +
            "this shape. Critical formatting rules: use standard straight double-quote characters only " +
            "(never curly/smart quotes); put each piece of information directly into its own named field; " +
            "never collapse the analysis into one long paragraph inside a single field; fill in every " +
            "field, use empty arrays/strings only where genuinely not applicable; keep executiveSummary " +
            $"under 250 words. previousVersion is {(previousAnalysis is null ? latestAnalysisVersion : latestAnalysisVersion - 1)} " +
            $"and currentVersion is {latestAnalysisVersion + 1}:");
        sb.AppendLine("""
            {
              "comparison": {
                "previousVersion": number,
                "currentVersion": number,
                "majorImprovements": [string],
                "resolvedDirectorComments": [string],
                "remainingRisks": [string]
              },
              "analysis": {
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
            }
            """);

        return sb.ToString();
    }

    private static void AppendAnalysis(StringBuilder sb, AiAnalysisResultDto analysis)
    {
        var similarity = analysis.Similarity;
        sb.AppendLine($"Similarity — Similar Request Found: {similarity.SimilarRequestFound}, Similarity%: {similarity.SimilarityPercentage}, " +
            $"Previous Request: {similarity.PreviousRequestNo ?? "none"} ({similarity.PreviousRequestTitle ?? "n/a"}), " +
            $"Reuse Possible: {similarity.ReusePossible}, Reuse%: {similarity.ReusePercentage}, " +
            $"Estimated Development Saving: {similarity.EstimatedDevelopmentSaving}, Summary: {similarity.Summary}");
        sb.AppendLine($"Current Business Process: {analysis.CurrentBusinessProcess.Description}");
        sb.AppendLine("Bottlenecks:");
        foreach (var b in analysis.Bottlenecks)
            sb.AppendLine($"- {b.Title}: {b.Description} (Impact: {b.BusinessImpact}, Severity: {b.Severity})");
        sb.AppendLine("Root Causes:");
        foreach (var r in analysis.RootCauses)
            sb.AppendLine($"- {r.Title}: {r.Description}");
        sb.AppendLine($"Director Perspective — Business Value: {analysis.DirectorPerspective.BusinessValue}");
        sb.AppendLine($"Director Perspective — Risk Reduction: {analysis.DirectorPerspective.RiskReduction}");
        sb.AppendLine($"Executive Summary: {analysis.ExecutiveSummary}");
    }
}
