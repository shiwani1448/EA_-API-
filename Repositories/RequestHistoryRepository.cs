using Jarvis5.Data;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class RequestHistoryRepository : IRequestHistoryRepository
{
    private readonly AppDbContext _context;

    public RequestHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SCIHRequestHistory history, CancellationToken ct = default) =>
        await _context.RequestHistories.AddAsync(history, ct);

    public Task<List<SCIHRequestHistory>> GetByRequestIdAsync(long requestId, CancellationToken ct = default) =>
        _context.RequestHistories
            .AsNoTracking()
            .Where(h => h.RequestId == requestId)
            .OrderBy(h => h.ActionDate).ThenBy(h => h.Id)
            .ToListAsync(ct);

    public Task<SCIHRequestHistory?> GetLatestByRequestIdAsync(long requestId, CancellationToken ct = default) =>
        _context.RequestHistories
            .AsNoTracking()
            .Where(h => h.RequestId == requestId)
            .OrderByDescending(h => h.ActionDate).ThenByDescending(h => h.Id)
            .FirstOrDefaultAsync(ct);
}
