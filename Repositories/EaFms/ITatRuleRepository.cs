using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface ITatRuleRepository
{
    IQueryable<TatRule> Query();
    Task<TatRule?> GetForUpdateAsync(long id, CancellationToken ct);
    Task<List<TatRule>> GetApplicableAsync(long moduleId, string type, string subtype, CancellationToken ct);
    /// <summary>
    /// Exact module + Type + TaskType match among active rules that have NO subtype (Delegation).
    /// No fallback to broader rules. TaskType is Actual/Review/Rework — see DelegationTaskType.
    /// </summary>
    Task<List<TatRule>> GetApplicableByTypeOnlyAsync(long moduleId, string type, string taskType, CancellationToken ct);
    Task<List<TatRule>> GetApplicableForApprovalAsync(long moduleId, string? type, string? subtype, CancellationToken ct);
    /// <summary>
    /// Approval's own per-phase TAT resolution: tries an EXACT module + Type + TaskType match (no
    /// Subtype), the same shape Delegation uses for its Actual/Review/Rework rules, via the
    /// UX_ea_tat_rules_ActiveApprovalTaskTypeClassification index. Falls back to the ordinary,
    /// taskType-agnostic GetApplicableForApprovalAsync cascade when no exact match exists. Soft lookup
    /// like ResolvePhaseTatAsync's convention — 0 or &gt;1 rows is "no TAT for this phase", never throws.
    /// </summary>
    Task<List<TatRule>> GetApplicableForApprovalPhaseAsync(long moduleId, string? type, string? subtype, string taskType, CancellationToken ct);
    Task AddAsync(TatRule rule, CancellationToken ct);
}
