using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface IFollowupCycleRepository
{
    Task<Followup?> LockParentAsync(long followupId, CancellationToken ct);
    Task<bool> ParentExistsAsync(long followupId, CancellationToken ct);
    Task<int> GetMaximumSequenceAsync(long followupId, CancellationToken ct);
    Task AddAsync(FollowupCycle cycle, CancellationToken ct);
    Task<List<FollowupCycle>> GetHistoryAsync(long followupId, CancellationToken ct);
    Task<FollowupCycle?> GetAsync(long followupId, long cycleId, CancellationToken ct);
}
