using System.Text;
using Jarvis5.Common;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;

namespace Jarvis5.Services.Ai;

public class SolutionDesignPromptBuilder : ISolutionDesignPromptBuilder
{
    public string PromptVersion => "v1";

    public string BuildSystemPrompt() => """
        You are a Senior Enterprise Solution Architect with more than 20 years of
        experience designing enterprise software.

        You also think as a:
        - Technical Architect
        - Business Architect
        - Digital Transformation Consultant
        - Process Improvement Consultant

        You think from:
        - Business
        - Operations
        - Architecture
        - Scalability
        - Security
        - Maintainability
        - Future Growth

        Do not generate source code.
        Do not generate SQL.
        Do not generate API implementation.

        Design enterprise level software. Design how the solution should work, not how
        to build it.

        Always check whether similar company solutions already exist.
        Reuse existing company assets whenever possible.
        Avoid duplicate development.
        Always think about the next five years.
        Think how this module can grow.

        Return only JSON.
        """;

    public string BuildUserPrompt(SCIHRequest request, AiAnalysisResultDto analysis, List<PreviousSolutionDesignDto> knowledgeBase)
    {
        var painPoints = JsonHelper.DeserializeList<Dtos.PainPointDto>(request.PainPointsJson);

        var sb = new StringBuilder();

        sb.AppendLine("Create a complete enterprise solution design.");
        sb.AppendLine("------------------------------------------------");
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
        sb.AppendLine("APPROVED ANALYSIS (business context, director notes, business summary)");
        sb.AppendLine($"Current Business Process: {analysis.CurrentBusinessProcess.Description}");
        sb.AppendLine("Bottlenecks:");
        foreach (var b in analysis.Bottlenecks)
            sb.AppendLine($"- {b.Title}: {b.Description} (Impact: {b.BusinessImpact}, Severity: {b.Severity})");
        sb.AppendLine("Root Causes:");
        foreach (var r in analysis.RootCauses)
            sb.AppendLine($"- {r.Title}: {r.Description}");
        sb.AppendLine("Director Notes:");
        sb.AppendLine($"- Business Value: {analysis.DirectorPerspective.BusinessValue}");
        sb.AppendLine($"- Expected Benefits: {analysis.DirectorPerspective.ExpectedBenefits}");
        sb.AppendLine($"- Risk Reduction: {analysis.DirectorPerspective.RiskReduction}");
        sb.AppendLine($"- Future Business Growth: {analysis.DirectorPerspective.FutureBusinessGrowth}");
        sb.AppendLine($"Business Summary (Executive Summary of Analysis): {analysis.ExecutiveSummary}");
        sb.AppendLine("Future Readiness Notes:");
        sb.AppendLine($"- Automation Opportunities: {string.Join("; ", analysis.FutureReadiness.AutomationOpportunities)}");
        sb.AppendLine($"- AI Opportunities: {string.Join("; ", analysis.FutureReadiness.AiOpportunities)}");
        sb.AppendLine($"- Future Integrations: {string.Join("; ", analysis.FutureReadiness.FutureIntegrations)}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("COMPANY KNOWLEDGE BASE (previous solution designs — reuse over rebuild)");
        if (knowledgeBase.Count == 0)
        {
            sb.AppendLine("No similar previous solution design was found.");
        }
        else
        {
            foreach (var k in knowledgeBase)
            {
                sb.AppendLine($"- {k.RequestNo}: {k.Title} (Department: {k.DepartmentId}, Status: {k.Status}, Similarity: {k.SimilarityScore}%)");
                sb.AppendLine($"  Previous Solution: {k.SolutionTitle} — {k.SolutionDescription}");
                if (k.ModuleNames.Count > 0)
                    sb.AppendLine($"  Existing Modules: {string.Join("; ", k.ModuleNames)}");
                if (k.ReusableApis.Count > 0)
                    sb.AppendLine($"  Existing APIs: {string.Join("; ", k.ReusableApis)}");
                if (k.ReusableDatabaseTables.Count > 0)
                    sb.AppendLine($"  Existing Database Tables: {string.Join("; ", k.ReusableDatabaseTables)}");
                if (k.ReusableUiComponents.Count > 0)
                    sb.AppendLine($"  Existing UI Components: {string.Join("; ", k.ReusableUiComponents)}");
                if (k.ReusableFlowcharts.Count > 0)
                    sb.AppendLine($"  Existing Flowcharts: {string.Join("; ", k.ReusableFlowcharts)}");
                if (k.ReusableDocuments.Count > 0)
                    sb.AppendLine($"  Existing SOP/Documents: {string.Join("; ", k.ReusableDocuments)}");
            }
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Now design the solution. Return ONLY a single JSON object with exactly this " +
            "shape. Critical formatting rules: use standard straight double-quote characters only " +
            "(never curly/smart quotes); put each piece of information directly into its own named " +
            "field; never collapse the whole design into one long paragraph inside a single field; " +
            "fill in every field, use empty arrays/strings only where genuinely not applicable; " +
            "prefer listing existing company assets in reusableAssets over inventing new ones in " +
            "newComponents whenever the knowledge base above already has a matching asset; " +
            "keep executiveSummary under 300 words:");
        sb.AppendLine("""
            {
              "solutionOverview": { "title": string, "description": string },
              "businessSolution": { "futureProcess": string },
              "moduleBreakdown": [ { "module": string, "purpose": string, "responsibilities": string, "dependencies": [string] } ],
              "reusableAssets": { "apis": [string], "databaseTables": [string], "modules": [string], "uiComponents": [string], "flowcharts": [string], "documents": [string] },
              "newComponents": { "apis": [string], "tables": [string], "uiScreens": [string], "reports": [string], "dashboards": [string] },
              "workflow": { "steps": [string] },
              "userRoles": [ { "role": string, "responsibility": string } ],
              "validationRules": [string],
              "businessRules": [string],
              "riskAnalysis": { "technical": [string], "business": [string], "operational": [string], "scalability": [string] },
              "futureEnhancements": [string],
              "directorRecommendation": string,
              "executiveSummary": string
            }
            """);

        return sb.ToString();
    }
}
