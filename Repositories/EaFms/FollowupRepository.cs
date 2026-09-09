using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class FollowupRepository : IFollowupRepository
{
    private readonly EaFmsDbContext _context;

    public FollowupRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Followup followup, CancellationToken ct = default) =>
        await _context.Followups.AddAsync(followup, ct);

    public Task<Followup?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _context.Followups.FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, ct);

    public Task<List<Followup>> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default) =>
        _context.Followups
            .AsNoTracking()
            .Where(f => f.IntakeRequestId == intakeRequestId && !f.IsDeleted)
            .OrderBy(f => f.DueAt)
            .ToListAsync(ct);

    public Task<List<Followup>> GetOpenFollowupsAsync(CancellationToken ct = default) =>
        _context.Followups
            .AsNoTracking()
            .Where(f => !f.IsDeleted && f.CompletedAt == null)
            .OrderBy(f => f.DueAt)
            .ToListAsync(ct);

    public Task<List<Followup>> GetBySourceAsync(long moduleId, string recordId, CancellationToken ct = default) =>
        _context.Followups.AsNoTracking()
            .Where(f => !f.IsDeleted && f.BusinessModuleId == moduleId && f.BusinessRecordId == recordId)
            .OrderBy(f => f.DueAt).ThenByDescending(f => f.Id).ToListAsync(ct);

    public async Task<PagedResult<Followup>> GetPagedAsync(FollowupListQueryDto filter, DateTime now, CancellationToken ct = default)
    {
        var query = _context.Followups.AsNoTracking().Where(f => !f.IsDeleted);
        if (filter.BusinessModuleId.HasValue) query = query.Where(f => f.BusinessModuleId == filter.BusinessModuleId);
        if (filter.BusinessRecordId != null) query = query.Where(f => f.BusinessRecordId == filter.BusinessRecordId);
        if (filter.AssignedToId != null) query = query.Where(f => f.AssignedToId == filter.AssignedToId);
        if (filter.WaitingOnId != null) query = query.Where(f => f.WaitingOnId == filter.WaitingOnId);
        if (filter.ResponseOwnerId != null) query = query.Where(f => f.ResponseOwnerId == filter.ResponseOwnerId);
        if (filter.PriorityLevelId.HasValue) query = query.Where(f => f.PriorityLevelId == filter.PriorityLevelId);
        var completed = filter.IsCompleted;
        if (filter.Status != null) completed = string.Equals(filter.Status, "Completed", StringComparison.OrdinalIgnoreCase);
        if (completed.HasValue) query = query.Where(f => (f.CompletedAt != null) == completed.Value);
        if (filter.IsOverdue.HasValue) query = query.Where(f => (f.CompletedAt == null && f.DueAt != default(DateTime) && f.DueAt < now) == filter.IsOverdue.Value);
        if (filter.DueFrom.HasValue) query = query.Where(f => f.DueAt >= filter.DueFrom.Value);
        if (filter.DueTo.HasValue) query = query.Where(f => f.DueAt <= filter.DueTo.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(f => (f.Subject != null && f.Subject.ToLower().Contains(term))
                || (f.Note != null && f.Note.ToLower().Contains(term))
                || (f.AssignedToName != null && f.AssignedToName.ToLower().Contains(term))
                || (f.WaitingOnName != null && f.WaitingOnName.ToLower().Contains(term))
                || (f.WaitingOnExternal != null && f.WaitingOnExternal.ToLower().Contains(term))
                || (f.ResponseOwnerName != null && f.ResponseOwnerName.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(f => f.DueAt).ThenByDescending(f => f.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<Followup> { Items = items, PageNumber = filter.Page, PageSize = filter.PageSize, TotalCount = total };
    }

    public void Update(Followup followup) => _context.Followups.Update(followup);
}
