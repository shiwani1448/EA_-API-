namespace Jarvis5.Dtos.Analysis;

/// <summary>Exact shape of the AI-generated (and later user-edited) analysis.
/// Field names/casing follow the spec's "Expected JSON Response" verbatim.</summary>
public class AiAnalysisResultDto
{
    public AiSimilarityDto Similarity { get; set; } = new();
    public AiCurrentBusinessProcessDto CurrentBusinessProcess { get; set; } = new();
    public List<AiBottleneckDto> Bottlenecks { get; set; } = new();
    public List<AiRootCauseDto> RootCauses { get; set; } = new();
    public List<AiImpactPriorityDto> ImpactPriority { get; set; } = new();
    public AiDirectorPerspectiveDto DirectorPerspective { get; set; } = new();
    public AiFutureReadinessDto FutureReadiness { get; set; } = new();
    public AiRecommendationsDto Recommendations { get; set; } = new();
    public string ExecutiveSummary { get; set; } = string.Empty;
}

public class AiSimilarityDto
{
    public bool SimilarRequestFound { get; set; }
    public double SimilarityPercentage { get; set; }
    public string? PreviousRequestNo { get; set; }
    public string? PreviousRequestTitle { get; set; }
    public bool ReusePossible { get; set; }
    public double ReusePercentage { get; set; }
    public string? EstimatedDevelopmentSaving { get; set; }
    public string? Summary { get; set; }
}

public class AiCurrentBusinessProcessDto
{
    public string Description { get; set; } = string.Empty;
}

public class AiBottleneckDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AffectedUsers { get; set; } = string.Empty;
    public string BusinessImpact { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AiRootCauseDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Category { get; set; } = new();
}

public class AiImpactPriorityDto
{
    public string Priority { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AiDirectorPerspectiveDto
{
    public string BusinessValue { get; set; } = string.Empty;
    public string ExpectedBenefits { get; set; } = string.Empty;
    public string RiskReduction { get; set; } = string.Empty;
    public string FutureBusinessGrowth { get; set; } = string.Empty;
}

public class AiFutureReadinessDto
{
    public List<string> FutureChallenges { get; set; } = new();
    public List<string> FutureDepartments { get; set; } = new();
    public List<string> FutureIntegrations { get; set; } = new();
    public List<string> AutomationOpportunities { get; set; } = new();
    public List<string> AiOpportunities { get; set; } = new();
    public List<string> ReportingSuggestions { get; set; } = new();
}

public class AiRecommendationsDto
{
    public List<string> Immediate { get; set; } = new();
    public List<string> MediumTerm { get; set; } = new();
    public List<string> LongTerm { get; set; } = new();
}
