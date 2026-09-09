using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class MeetingRepository : IMeetingRepository
{
    private readonly EaFmsDbContext _context;

    public MeetingRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Meeting meeting, CancellationToken ct = default) =>
        await _context.AddAsync(meeting, ct);

    public Task<Meeting?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _context.Meetings.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted, ct);

    public Task UpdateAsync(Meeting meeting)
    {
        _context.Meetings.Update(meeting);
        return Task.CompletedTask;
    }

    public Task<List<Meeting>> QueryAsync(Func<IQueryable<Meeting>, IQueryable<Meeting>>? query = null, CancellationToken ct = default)
    {
        var q = _context.Meetings.AsNoTracking().Where(m => !m.IsDeleted).AsQueryable();
        if (query != null) q = query(q);
        return q.ToListAsync(ct);
    }
}
