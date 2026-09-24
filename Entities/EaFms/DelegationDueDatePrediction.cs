namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI due-date prediction for a Delegation, shaped exactly like
/// DelegationAiDueDatePredictionResponseDto — every field is scalar, no jsonb needed.
/// Applied via /ai/predict-due-date/apply, which writes EndDate onto the real
/// ea_delegations row — AppliedDueDate records exactly what was written (the EA's
/// reviewed/edited date, which may differ from SuggestedDueDate).
/// </summary>
public class DelegationDueDatePrediction
{
    public long Id { get; set; }
    public long DelegationId { get; set; }

    /// <summary>Null when there was nothing to estimate from (Basis == "None").</summary>
    public DateTime? SuggestedDueDate { get; set; }
    /// <summary>ConfiguredTat | HistoricalAverage | None.</summary>
    public string Basis { get; set; } = "None";
    public string? Explanation { get; set; }
    public string? WarningMessage { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime? AppliedDueDate { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
