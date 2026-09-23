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
    Task AddAsync(TatRule rule, CancellationToken ct);
}
