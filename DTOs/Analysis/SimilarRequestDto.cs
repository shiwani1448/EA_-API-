namespace Jarvis5.Dtos.Analysis;

/// <summary>One "previous similar request" fed into the AI prompt context.</summary>
public class SimilarRequestDto
{
    public long RequestId { get; set; }
    public string RequestNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ExpectedBenefit { get; set; } = string.Empty;
    public List<string> PainPointTitles { get; set; } = new();
    public double SimilarityScore { get; set; }
    public string? PreviousAnalysisExecutiveSummary { get; set; }
}
