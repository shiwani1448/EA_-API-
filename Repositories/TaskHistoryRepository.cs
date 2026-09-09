using Jarvis5.Data;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class TaskHistoryRepository : ITaskHistoryRepository
{
    private readonly AppDbContext _context;

    public TaskHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SCIHTaskHistory history, CancellationToken ct = default) =>
        await _context.TaskHistories.AddAsync(history, ct);

    public Task<List<SCIHTaskHistory>> GetByTaskIdAsync(long taskId, CancellationToken ct = default) =>
        _context.TaskHistories
            .AsNoTracking()
            .Where(h => h.TaskId == taskId)
            .OrderBy(h => h.ActionDate).ThenBy(h => h.Id)
            .ToListAsync(ct);

    public Task<List<SCIHTaskHistory>> GetByRequestIdAsync(long requestId, CancellationToken ct = default) =>
        _context.TaskHistories
            .AsNoTracking()
            .Where(h => h.RequestId == requestId)
            .OrderBy(h => h.ActionDate).ThenBy(h => h.Id)
            .ToListAsync(ct);
}
