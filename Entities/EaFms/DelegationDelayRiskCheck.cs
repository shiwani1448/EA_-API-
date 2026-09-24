namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI delay-risk check for a Delegation, shaped exactly like
/// DelegationAiDelayRiskResponseDto — every field is scalar, no jsonb needed. Purely
/// advisory: there is no confirm/apply step for a risk assessment in the real flow (the
/// suggested nudge message is copy-sent by the EA manually, outside this system).
/// </summary>
public class DelegationDelayRiskCheck
{
    public long Id { get; set; }
    public long DelegationId { get; set; }

    /// <summary>Low | Medium | High.</summary>
    public string RiskLevel { get; set; } = "Low";
    public string? Reasoning { get; set; }
    public string? SuggestedNudgeMessage { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
