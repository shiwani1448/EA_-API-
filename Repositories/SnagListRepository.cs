using Jarvis5.Data;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class SnagListRepository : ISnagListRepository
{
    private readonly AppDbContext _context;

    public SnagListRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<SCIHSnagList?> GetByIdAsync(long snagId, CancellationToken ct = default) =>
        _context.SnagLists.FirstOrDefaultAsync(s => s.Id == (int)snagId, ct);

    public Task<List<SCIHSnagList>> GetAllAsync(CancellationToken ct = default) =>
        _context.SnagLists
            .AsNoTracking()
            .OrderBy(s => s.CreationDate).ThenBy(s => s.Id)
            .ToListAsync(ct);

    public async Task AddAsync(SCIHSnagList snag, CancellationToken ct = default) =>
        await _context.SnagLists.AddAsync(snag, ct);

    public void Update(SCIHSnagList snag) => _context.SnagLists.Update(snag);
}
