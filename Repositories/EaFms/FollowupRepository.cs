using Jarvis5.Common;
using Jarvis5.Common.EaFms;
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
        var query = ApplyFilters(filter, now);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(f => f.DueAt).ThenByDescending(f => f.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<Followup> { Items = items, PageNumber = filter.Page, PageSize = filter.PageSize, TotalCount = total };
    }

    public async Task<FollowupSummaryResponseDto> GetSummaryAsync(FollowupListQueryDto filter, DateTime now, DateTime indiaToday, CancellationToken ct = default)
    {
        var tomorrow = indiaToday.AddDays(1);
        var query = ApplyFilters(filter, now);
        var result = await query.GroupBy(_ => 1).Select(group => new FollowupSummaryResponseDto
        {
            Total = group.Count(),
            Pending = group.Count(f => f.CompletedAt == null),
            Completed = group.Count(f => f.CompletedAt != null),
            DueToday = group.Count(f => f.CompletedAt == null && f.DueAt >= indiaToday && f.DueAt < tomorrow),
            Overdue = group.Count(f => f.CompletedAt == null && f.DueAt != default && f.DueAt < now),
            UpcomingReminders = group.Count(f => f.CompletedAt == null && f.ReminderAt.HasValue && f.ReminderAt.Value > now),
            Escalated = group.Count(f => _context.Escalations.Any(e => !e.IsDeleted && e.FollowupId == f.Id)),
            NotStarted = group.Count(f => f.EaTaskId != null
                && _context.Tasks.Any(t => t.Id == f.EaTaskId && t.ExecutionStatus == EaTaskExecutionStatus.NotStarted))
        }).SingleOrDefaultAsync(ct);
        return result ?? new FollowupSummaryResponseDto();
    }

    private IQueryable<Followup> ApplyFilters(FollowupListQueryDto filter, DateTime now)
    {
        var query = _context.Followups.AsNoTracking().Where(f => !f.IsDeleted);
        if (filter.BusinessModuleId.HasValue) query = query.Where(f => f.BusinessModuleId == filter.BusinessModuleId);
        if (filter.BusinessRecordId != null) query = query.Where(f => f.BusinessRecordId == filter.BusinessRecordId);
        if (filter.DoerId != null) query = query.Where(f => f.DoerId == filter.DoerId);
        if (filter.WaitingOnId != null) query = query.Where(f => f.WaitingOnId == filter.WaitingOnId);
        if (filter.ResponseOwnerId != null) query = query.Where(f => f.ResponseOwnerId == filter.ResponseOwnerId);
        if (filter.PriorityLevelId.HasValue) query = query.Where(f => f.PriorityLevelId == filter.PriorityLevelId);
        var completed = filter.IsCompleted;
        if (filter.Status != null) completed = string.Equals(filter.Status, "Completed", StringComparison.OrdinalIgnoreCase);
        if (completed.HasValue) query = query.Where(f => (f.CompletedAt != null) == completed.Value);
        if (filter.IsOverdue.HasValue) query = query.Where(f => (f.CompletedAt == null && f.DueAt != default(DateTime) && f.DueAt < now) == filter.IsOverdue.Value);
        if (filter.DueFrom.HasValue) query = query.Where(f => f.DueAt >= filter.DueFrom.Value);
        if (filter.DueTo.HasValue) query = query.Where(f => f.DueAt <= filter.DueTo.Value);
        if (filter.ReminderFrom.HasValue) query = query.Where(f => f.ReminderAt >= filter.ReminderFrom.Value);
        if (filter.ReminderTo.HasValue) query = query.Where(f => f.ReminderAt <= filter.ReminderTo.Value);
        if (filter.ReminderRecipientEmployeeId != null) query = query.Where(f => f.ReminderRecipientEmployeeId == filter.ReminderRecipientEmployeeId);
        if (filter.ReminderSendWhatsApp.HasValue) query = query.Where(f => f.ReminderSendWhatsApp == filter.ReminderSendWhatsApp.Value);
        if (filter.ReminderSendEmail.HasValue) query = query.Where(f => f.ReminderSendEmail == filter.ReminderSendEmail.Value);
        if (filter.IsEscalated.HasValue) query = query.Where(f => _context.Escalations.Any(e => !e.IsDeleted && e.FollowupId == f.Id) == filter.IsEscalated.Value);
        if (filter.EscalationLevelId.HasValue) query = query.Where(f => _context.Escalations.Any(e => !e.IsDeleted && e.FollowupId == f.Id && e.EscalationLevelId == filter.EscalationLevelId.Value));
        if (!string.IsNullOrWhiteSpace(filter.View))
        {
            var view = filter.View.Trim();
            if (string.Equals(view, "notstarted", StringComparison.OrdinalIgnoreCase))
                query = query.Where(f => f.EaTaskId != null && _context.Tasks.Any(t => t.Id == f.EaTaskId && t.ExecutionStatus == EaTaskExecutionStatus.NotStarted));
            else if (string.Equals(view, "started", StringComparison.OrdinalIgnoreCase))
                query = query.Where(f => f.EaTaskId != null && _context.Tasks.Any(t => t.Id == f.EaTaskId && t.ExecutionStatus == EaTaskExecutionStatus.InProgress));
            else
                throw new BadRequestException("View must be notstarted or started.");
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(f => (f.Subject != null && f.Subject.ToLower().Contains(term))
                || (f.Note != null && f.Note.ToLower().Contains(term))
                || (f.DoerName != null && f.DoerName.ToLower().Contains(term))
                || (f.WaitingOnName != null && f.WaitingOnName.ToLower().Contains(term))
                || (f.WaitingOnExternal != null && f.WaitingOnExternal.ToLower().Contains(term))
                || (f.ResponseOwnerName != null && f.ResponseOwnerName.ToLower().Contains(term)));
        }
        return query;
    }
    public void Update(Followup followup) => _context.Followups.Update(followup);
}
