using Jarvis5.Data;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class StageMasterRepository : IStageMasterRepository
{
    private readonly AppDbContext _context;

    public StageMasterRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<SCIHStageMaster>> GetAllActiveAsync(CancellationToken ct = default) =>
        _context.StageMasters
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

    public Task<List<SCIHStageMaster>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return Task.FromResult(new List<SCIHStageMaster>());
        return _context.StageMasters
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .ToListAsync(ct);
    }
}
