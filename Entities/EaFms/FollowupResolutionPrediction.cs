namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI resolution-time prediction for a Followup — purely advisory, never applied to any
/// field (Followup has no "predicted resolution" column of its own), same "log only"
/// convention as ApprovalStatusSummary/DelegationDelayRiskCheck. PredictedResolutionDate is
/// computed here in C# from real historical completed Followups of the same Type — Claude is
/// only ever asked to explain the already-computed date, never to invent one.
/// </summary>
public class FollowupResolutionPrediction
{
    public long Id { get; set; }
    public long FollowupId { get; set; }

    public DateTime? PredictedResolutionDate { get; set; }
    /// <summary>HistoricalAverage | None.</summary>
    public string Basis { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public string? WarningMessage { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
