namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI owner suggestion for a Delegation, shaped exactly like
/// DelegationAiOwnerSuggestionResponseDto — every field is scalar, no jsonb needed. Applied
/// via /ai/suggest-owner/apply, which writes DoerId/DoerNameSnapshot onto the real
/// ea_delegations row — AppliedDoerId/AppliedDoerName record exactly what was written (the
/// EA's reviewed/edited choice, which may differ from SuggestedDoerId).
/// </summary>
public class DelegationOwnerSuggestion
{
    public long Id { get; set; }
    public long DelegationId { get; set; }

    /// <summary>Null when there was no history to suggest from.</summary>
    public string? SuggestedDoerId { get; set; }
    public string? SuggestedDoerName { get; set; }
    public int HistoricalSampleSize { get; set; }
    public string? Reasoning { get; set; }
    public string? WarningMessage { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    public string? AppliedDoerId { get; set; }
    public string? AppliedDoerName { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
