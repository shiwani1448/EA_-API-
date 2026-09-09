namespace Jarvis5.Dtos.Analysis;

/// <summary>Exact shape of the AI rework response: a short comparison against the
/// previous version, plus a full analysis in the same shape as AiAnalysisResultDto.</summary>
public class AiAnalysisReworkResultDto
{
    public AiReworkComparisonDto Comparison { get; set; } = new();
    public AiAnalysisResultDto Analysis { get; set; } = new();
}

public class AiReworkComparisonDto
{
    public int PreviousVersion { get; set; }
    public int CurrentVersion { get; set; }
    public List<string> MajorImprovements { get; set; } = new();
    public List<string> ResolvedDirectorComments { get; set; } = new();
    public List<string> RemainingRisks { get; set; } = new();
}
