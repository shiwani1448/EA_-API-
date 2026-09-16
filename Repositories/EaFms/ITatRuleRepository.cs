using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface ITatRuleRepository
{
    IQueryable<TatRule> Query();
    Task<TatRule?> GetForUpdateAsync(long id, CancellationToken ct);
    Task<List<TatRule>> GetApplicableAsync(long moduleId, string type, string subtype, CancellationToken ct);
    Task<List<TatRule>> GetApplicableForApprovalAsync(long moduleId, string? type, string? subtype, CancellationToken ct);
    Task AddAsync(TatRule rule, CancellationToken ct);
}
