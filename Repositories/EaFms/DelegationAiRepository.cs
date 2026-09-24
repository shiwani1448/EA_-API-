using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class DelegationAiRepository(EaFmsDbContext db) : IDelegationAiRepository
{
    public async Task<List<(string DoerId, string? DoerName, int Count)>> GetTopDoersByTypeAsync(long excludeDelegationId, string delegationType, CancellationToken ct)
    {
        var rows = await db.Delegations.AsNoTracking()
            .Where(d => !d.IsDeleted && d.Id != excludeDelegationId && d.DelegationType == delegationType)
            .GroupBy(d => d.DoerId)
            .Select(g => new
            {
                DoerId = g.Key,
                Count = g.Count(),
                DoerName = g.OrderByDescending(x => x.CreatedDate).Select(x => x.DoerNameSnapshot).FirstOrDefault(),
            })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync(ct);

        return rows.Select(r => (r.DoerId, r.DoerName, r.Count)).ToList();
    }

    public async Task<List<(DateTime StartedAt, DateTime CompletedAt)>> GetCompletedDurationSamplesAsync(long excludeDelegationId, string delegationType, CancellationToken ct)
    {
        var rows = await db.Delegations.AsNoTracking()
            .Where(d => !d.IsDeleted && d.Id != excludeDelegationId && d.DelegationType == delegationType
                && d.Status == DelegationStatus.Completed && d.StartedAt != null && d.CompletedAt != null)
            .Select(d => new { d.StartedAt, d.CompletedAt })
            .ToListAsync(ct);

        return rows.Select(r => (r.StartedAt!.Value, r.CompletedAt!.Value)).ToList();
    }

    public Task<long?> ResolveDelegationModuleIdAsync(CancellationToken ct) =>
        db.BusinessModules.AsNoTracking()
            .Where(m => !m.IsDeleted && m.IsActive && m.Name == "Delegation")
            .Select(m => (long?)m.Id)
            .FirstOrDefaultAsync(ct);
}
