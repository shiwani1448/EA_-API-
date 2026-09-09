namespace Jarvis5.Entities;

/// <summary>One analysis per request. Written by Generate Analysis (AI draft) and
/// then overwritten by Save Analysis once the user has reviewed/edited it.</summary>
public class SCIHAnalysis
{
    public long Id { get; set; }
    public long RequestId { get; set; }

    /// <summary>Raw JSON object — see AiAnalysisResultDto for shape (similarity,
    /// currentBusinessProcess, bottlenecks, rootCauses, impactPriority,
    /// directorPerspective, futureReadiness, recommendations, executiveSummary).</summary>
    public string AnalysisJson { get; set; } = "{}";

    public string? GeneratedByModel { get; set; }

    /// <summary>DRAFT | GENERATING | COMPLETED | REWORK | REWORK_COMPLETED | APPROVED
    /// — see SCIHAnalysisStatus. Indicates whether this version's AI generation is
    /// done, separate from the request-level workflow status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>1 for the first analysis on a request; incremented only by the
    /// Approval-rework path (POST analysis/rework), which always inserts a new
    /// row instead of overwriting — earlier versions are immutable.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Cumulative rework count carried over from SCIH_Approval at the
    /// time this version was produced (0 for the original, non-reworked version).</summary>
    public int ReworkCount { get; set; }

    /// <summary>Id of the SCIHAnalysis row this version improved upon, or null
    /// for the original (Version 1) row.</summary>
    public long? PreviousAnalysisId { get; set; }

    /// <summary>Raw JSON object — see AiReworkComparisonDto for shape. Only set
    /// for versions produced by the rework path; null for the original.</summary>
    public string? ComparisonJson { get; set; }

    public long CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
