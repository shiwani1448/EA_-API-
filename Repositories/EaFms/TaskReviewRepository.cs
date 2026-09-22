using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class TaskReviewRepository : ITaskReviewRepository
{
    private readonly EaFmsDbContext _context;

    public TaskReviewRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TaskReview review, CancellationToken ct = default) =>
        await _context.TaskReviews.AddAsync(review, ct);

    public async Task<TaskReview?> GetCurrentAsync(long eaTaskId, CancellationToken ct = default)
    {
        var current = await _context.TaskReviews
            .Where(r => r.EaTaskId == eaTaskId)
            .OrderByDescending(r => r.ReviewCycleNo)
            .FirstOrDefaultAsync(ct);
        // A module response may already have tracked this cycle before the task lock
        // was acquired. Re-read persisted values after a concurrent decision commits.
        if (current is not null && _context.Database.IsRelational())
            await _context.Entry(current).ReloadAsync(ct);
        return current;
    }

    public Task<List<TaskReview>> GetHistoryAsync(long eaTaskId, CancellationToken ct = default) =>
        _context.TaskReviews.AsNoTracking()
            .Where(r => r.EaTaskId == eaTaskId)
            .OrderBy(r => r.ReviewCycleNo)
            .ToListAsync(ct);

    public async Task<Dictionary<long, TaskReview>> GetCurrentBatchAsync(IReadOnlyCollection<long> eaTaskIds, CancellationToken ct = default)
    {
        if (eaTaskIds.Count == 0) return new();
        var rows = await _context.TaskReviews.AsNoTracking()
            .Where(r => eaTaskIds.Contains(r.EaTaskId))
            .Where(r => !_context.TaskReviews.Any(later =>
                later.EaTaskId == r.EaTaskId && later.ReviewCycleNo > r.ReviewCycleNo))
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.EaTaskId);
    }

    public void Update(TaskReview review) => _context.TaskReviews.Update(review);
}
