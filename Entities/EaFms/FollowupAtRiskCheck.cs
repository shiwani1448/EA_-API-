namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI at-risk assessment for a Followup — purely advisory, never applied anywhere, same
/// "log only" convention as DelegationDelayRiskCheck. RiskLevel (Low/Medium/High) is Claude's
/// own judgement, but it is given only real, already-computed facts (days overdue, follow-up
/// attempt count, current escalation state) — it never invents the underlying facts.
/// </summary>
public class FollowupAtRiskCheck
{
    public long Id { get; set; }
    public long FollowupId { get; set; }

    /// <summary>Low | Medium | High.</summary>
    public string RiskLevel { get; set; } = string.Empty;
    public string? Reasoning { get; set; }
    public string? SuggestedAction { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
