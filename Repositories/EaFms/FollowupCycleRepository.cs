using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class FollowupCycleRepository : IFollowupCycleRepository
{
    private readonly EaFmsDbContext _context;
    public FollowupCycleRepository(EaFmsDbContext context) => _context = context;

    // Caller must hold a transaction. PostgreSQL serializes cycle creators per parent,
    // including the empty-history case; the subsequent MAX sees the preceding commit.
    public async Task<Followup?> LockParentAsync(long followupId, CancellationToken ct)
    {
        var parents = await _context.Followups.FromSqlInterpolated(
            $"SELECT * FROM public.ea_followups WHERE \"Id\" = {followupId} AND NOT \"IsDeleted\" FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        return parents.SingleOrDefault();
    }

    public Task<bool> ParentExistsAsync(long followupId, CancellationToken ct) =>
        _context.Followups.AnyAsync(f => f.Id == followupId && !f.IsDeleted, ct);

    public async Task<int> GetMaximumSequenceAsync(long followupId, CancellationToken ct) =>
        await _context.FollowupCycles.Where(c => c.FollowupId == followupId)
            .MaxAsync(c => (int?)c.SequenceNumber, ct) ?? 0;

    public async Task AddAsync(FollowupCycle cycle, CancellationToken ct) =>
        await _context.FollowupCycles.AddAsync(cycle, ct);

    public Task<List<FollowupCycle>> GetHistoryAsync(long followupId, CancellationToken ct) =>
        _context.FollowupCycles.AsNoTracking()
            .Where(c => c.FollowupId == followupId && c.Followup != null && !c.Followup.IsDeleted)
            .OrderBy(c => c.SequenceNumber).ToListAsync(ct);

    public Task<FollowupCycle?> GetAsync(long followupId, long cycleId, CancellationToken ct) =>
        _context.FollowupCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId
            && c.FollowupId == followupId && c.Followup != null && !c.Followup.IsDeleted, ct);
}
