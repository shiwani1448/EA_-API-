namespace Jarvis5.Dtos.Analysis;

public class AnalysisDetailDto
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;

    public long Id { get; set; }
    public long RequestId { get; set; }
    public AiAnalysisResultDto Analysis { get; set; } = new();
    public int Version { get; set; } = 1;
    public int ReworkCount { get; set; }
    public long? PreviousAnalysisId { get; set; }

    /// <summary>DRAFT | GENERATING | COMPLETED | REWORK | REWORK_COMPLETED | APPROVED
    /// — this version's own generation status. See SCIHAnalysisStatus.</summary>
    public string AnalysisStatus { get; set; } = string.Empty;

    /// <summary>Only set for versions produced by the Approval-rework path.</summary>
    public AiReworkComparisonDto? Comparison { get; set; }

    public int CurrentStage { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;

    /// <summary>Request-level workflow status (e.g. ANALYSIS, ANALYSIS_REWORK,
    /// ANALYSIS_REWORK_COMPLETED) — see SCIHStatus. Distinct from AnalysisStatus above.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
