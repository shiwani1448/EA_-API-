using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class FollowupAiRepository(EaFmsDbContext db) : IFollowupAiRepository
{
    public async Task<List<(DateTime CreatedDate, DateTime CompletedAt)>> GetCompletedDurationSamplesByTypeAsync(long excludeFollowupId, string type, CancellationToken ct)
    {
        var rows = await db.Followups.AsNoTracking()
            .Where(f => !f.IsDeleted && f.Id != excludeFollowupId && f.Type == type && f.CompletedAt != null)
            .Select(f => new { f.CreatedDate, f.CompletedAt })
            .ToListAsync(ct);

        return rows.Select(r => (r.CreatedDate, r.CompletedAt!.Value)).ToList();
    }
}
