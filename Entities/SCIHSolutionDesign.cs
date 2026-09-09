namespace Jarvis5.Entities;

/// <summary>One solution design per request. Written by Generate Solution Design
/// (AI draft, auto-saved to prevent data loss) and then edited in place by
/// Update Solution Design until Approve locks it.</summary>
public class SCIHSolutionDesign
{
    public long Id { get; set; }
    public long RequestId { get; set; }

    /// <summary>Raw JSON object — see SolutionDesignResultDto for shape (solutionOverview,
    /// businessSolution, moduleBreakdown, reusableAssets, newComponents, workflow,
    /// userRoles, validationRules, businessRules, riskAnalysis, futureEnhancements,
    /// directorRecommendation, executiveSummary).</summary>
    public string SolutionJson { get; set; } = "{}";

    /// Raw JSON array — see AttachmentDto for shape.
    public string AttachmentsJson { get; set; } = "[]";

    public string? AIModel { get; set; }
    public string? PromptVersion { get; set; }
    public DateTime GeneratedAt { get; set; }
    public long GeneratedBy { get; set; }
    public bool IsEdited { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
