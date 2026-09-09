using Jarvis5.Data;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<SCIHTask?> GetByIdAsync(long taskId, CancellationToken ct = default) =>
        _context.Tasks.FirstOrDefaultAsync(t => t.Id == (int)taskId, ct);

    public Task<List<SCIHTask>> GetByRequestIdAsync(long requestId, CancellationToken ct = default)
    {
        var requestIdText = requestId.ToString();
        return _context.Tasks
            .AsNoTracking()
            .Where(t => t.RequestId == requestIdText)
            .OrderBy(t => t.CreationDate).ThenBy(t => t.Id)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SCIHTask task, CancellationToken ct = default) =>
        await _context.Tasks.AddAsync(task, ct);

    public void Update(SCIHTask task) => _context.Tasks.Update(task);
}
