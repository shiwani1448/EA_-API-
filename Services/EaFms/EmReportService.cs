using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>Read-only EM overview for the central EaTask created-date cohort.</summary>
public class EmReportService(EaFmsDbContext db) : IEmReportService
{
    private enum Performance { OnTime, Delayed, NotMeasured }
    private enum Bucket { NotStarted, InProgress, Paused, Completed, Cancelled }

    /// <summary>
    /// One cohort task with its classifications computed exactly once. Overview, Attention and Modules all
    /// aggregate these prepared rows, so they cannot disagree on status, pause or performance.
    /// Performance is null for Cancelled tasks (never measured).
    /// </summary>
    private sealed record PreparedTask(EaTask Task, Bucket Bucket, Performance? Performance, DateTime? Deadline, int? TatUsed);

    /// <summary>Counts and the two percentage formulas, shared by the overall overview and every module row.</summary>
    private sealed class Tally
    {
        public int Total, NotStarted, InProgress, Paused, Completed, Cancelled, OnTimeCompleted, DelayedCompleted, NotMeasuredCompleted, Delayed;

        public void Add(PreparedTask t)
        {
            Total++;
            switch (t.Bucket)
            {
                case Bucket.Completed: Completed++; break;
                case Bucket.Paused: Paused++; break;
                case Bucket.InProgress: InProgress++; break;
                case Bucket.NotStarted: NotStarted++; break;
                case Bucket.Cancelled: Cancelled++; break;
            }
            if (t.Bucket == Bucket.Cancelled) return;
            if (t.Performance == Performance.Delayed) Delayed++;
            if (t.Bucket != Bucket.Completed) return;
            if (t.Performance == Performance.OnTime) OnTimeCompleted++;
            else if (t.Performance == Performance.Delayed) DelayedCompleted++;
            else NotMeasuredCompleted++;
        }

        public decimal CompletionPercentage => Total - Cancelled == 0 ? 0 : decimal.Round(Completed * 100m / (Total - Cancelled), 2);
        public decimal OnTimeCompletionPercentage =>
            OnTimeCompleted + DelayedCompleted == 0 ? 0 : decimal.Round(OnTimeCompleted * 100m / (OnTimeCompleted + DelayedCompleted), 2);
    }

    private async Task<List<PreparedTask>> PrepareAsync(EmReportOverviewQueryDto query, CancellationToken ct,
        Func<IQueryable<EaTask>, IQueryable<EaTask>>? refine = null)
    {
        var cohort = BuildCohortQuery(query);
        if (refine is not null) cohort = refine(cohort);   // DB-side narrowing that does not depend on derived state
        var tasks = await cohort.Include(x => x.BusinessModule).ToListAsync(ct);
        if (tasks.Count == 0) return [];

        var pausesByWorkflow = await LoadPausesAsync(tasks, ct);
        var deadlines = await LoadDeadlinesAsync(tasks, ct);
        var now = Clock.UtcNowTz;
        var today = IndiaBusinessCalendar.Today;
        var prepared = new List<PreparedTask>(tasks.Count);
        foreach (var task in tasks)
        {
            var pauses = task.WorkflowInstanceId.HasValue && pausesByWorkflow.TryGetValue(task.WorkflowInstanceId.Value, out var grouped) ? grouped : [];
            var isPaused = task.ExecutionStatus == EaTaskExecutionStatus.InProgress && pauses.Any(x => x.EndAt is null);
            var bucket = task.ExecutionStatus switch
            {
                EaTaskExecutionStatus.Completed => Bucket.Completed,
                EaTaskExecutionStatus.InProgress when isPaused => Bucket.Paused,
                EaTaskExecutionStatus.InProgress => Bucket.InProgress,
                EaTaskExecutionStatus.Cancelled => Bucket.Cancelled,
                _ => Bucket.NotStarted
            };
            Performance? performance = bucket == Bucket.Cancelled ? null : ClassifyPerformance(task, pauses, deadlines, now, today);
            // Canonical current/final TAT minutes (null = no TAT applies). Cancelled tasks have no meaningful TAT position.
            int? tatUsed = bucket == Bucket.Cancelled ? null : EaTaskService.CalculateCurrentTatUsedMinutes(task, pauses, now);
            prepared.Add(new PreparedTask(task, bucket, performance, GetBusinessDeadline(task, deadlines), tatUsed));
        }
        return prepared;
    }

    public async Task<EmReportOverviewResponseDto> GetOverviewAsync(EmReportOverviewQueryDto query, CancellationToken ct)
    {
        var prepared = await PrepareAsync(query, ct);
        if (prepared.Count == 0) return new();
        var tally = new Tally();
        foreach (var t in prepared) tally.Add(t);
        return new EmReportOverviewResponseDto
        {
            TotalTasks = tally.Total, NotStarted = tally.NotStarted, InProgress = tally.InProgress, Paused = tally.Paused,
            Completed = tally.Completed, Cancelled = tally.Cancelled, CompletionPercentage = tally.CompletionPercentage,
            OnTimeCompleted = tally.OnTimeCompleted, DelayedCompleted = tally.DelayedCompleted,
            NotMeasuredCompleted = tally.NotMeasuredCompleted, OnTimeCompletionPercentage = tally.OnTimeCompletionPercentage,
            DelayedTasks = tally.Delayed
        };
    }

    public async Task<EmReportAttentionResponseDto> GetAttentionAsync(EmReportOverviewQueryDto query, CancellationToken ct)
    {
        var prepared = await PrepareAsync(query, ct);
        if (prepared.Count == 0) return new();

        var today = IndiaBusinessCalendar.Today;
        var result = new EmReportAttentionResponseDto();
        foreach (var t in prepared)
        {
            if (t.Bucket is not Bucket.Completed and not Bucket.Cancelled && t.Deadline?.Date == today) result.DueToday++;
            if (t.Performance == Performance.Delayed) result.Delayed++;
        }
        var sources = await LoadSourceInfoAsync(prepared, ct);
        result.TasksRequiringFollowup = sources.Count(x => x.Value.Pending > 0);
        result.OpenEscalations = sources.Values.Sum(x => x.OpenEscalations);
        return result;
    }

    public async Task<IReadOnlyList<EmReportModuleSummaryDto>> GetModulesAsync(EmReportOverviewQueryDto query, CancellationToken ct)
    {
        var prepared = await PrepareAsync(query, ct);
        if (prepared.Count == 0) return [];

        var sources = await LoadSourceInfoAsync(prepared, ct);
        var followupCounts = sources.Where(x => x.Value.Pending > 0).GroupBy(x => x.Key.BusinessModuleId).ToDictionary(g => g.Key, g => g.Count());
        var escalationCounts = sources.GroupBy(x => x.Key.BusinessModuleId).ToDictionary(g => g.Key, g => g.Sum(x => x.Value.OpenEscalations));

        // Grouping key is EaTask.BusinessModuleId. The name is the current BusinessModule master name
        // (loaded through the FK even for inactive modules), not the EaTask snapshot.
        return prepared.GroupBy(t => t.Task.BusinessModuleId).OrderBy(g => g.Key).Select(g =>
        {
            var tally = new Tally();
            foreach (var t in g) tally.Add(t);
            return new EmReportModuleSummaryDto
            {
                BusinessModuleId = g.Key,
                ModuleName = g.First().Task.BusinessModule?.Name ?? g.First().Task.ModuleName,
                TotalTasks = tally.Total, NotStarted = tally.NotStarted, InProgress = tally.InProgress, Paused = tally.Paused,
                Completed = tally.Completed, Cancelled = tally.Cancelled, CompletionPercentage = tally.CompletionPercentage,
                OnTimeCompleted = tally.OnTimeCompleted, DelayedCompleted = tally.DelayedCompleted,
                NotMeasuredCompleted = tally.NotMeasuredCompleted, OnTimeCompletionPercentage = tally.OnTimeCompletionPercentage,
                Delayed = tally.Delayed,
                TasksRequiringFollowup = followupCounts.GetValueOrDefault(g.Key),
                OpenEscalations = escalationCounts.GetValueOrDefault(g.Key)
            };
        }).ToList();
    }

    /// <summary>Per source task: non-deleted Followup rows, incomplete ones, and unresolved Escalation rows.</summary>
    private sealed class SourceInfo { public int Total; public int Pending; public int OpenEscalations; }

    /// <summary>
    /// Two batched queries for the whole prepared cohort, shared by Attention, Modules and the Task Register.
    /// Pending counts incomplete non-deleted Followup ROWS per source task (Attention counts sources with Pending &gt; 0,
    /// so a task with two pending Followups is one task). OpenEscalations counts unresolved non-deleted Escalation ROWS
    /// reachable through the source's non-deleted Followups.
    /// </summary>
    private async Task<Dictionary<SourceKey, SourceInfo>> LoadSourceInfoAsync(IReadOnlyCollection<PreparedTask> prepared, CancellationToken ct)
    {
        var sourceKeys = prepared.Select(t => new SourceKey(t.Task.BusinessModuleId, t.Task.BusinessRecordId)).ToHashSet();
        var result = sourceKeys.ToDictionary(k => k, _ => new SourceInfo());
        if (result.Count == 0) return result;
        var moduleIds = sourceKeys.Select(k => k.BusinessModuleId).Distinct().ToList();
        var recordIds = sourceKeys.Select(k => k.BusinessRecordId).Distinct().ToList();
        var followups = await db.Followups.AsNoTracking().Where(f => !f.IsDeleted
            && f.BusinessModuleId.HasValue && f.BusinessRecordId != null
            && moduleIds.Contains(f.BusinessModuleId.Value) && recordIds.Contains(f.BusinessRecordId))
            .Select(f => new { f.Id, f.BusinessModuleId, f.BusinessRecordId, f.CompletedAt }).ToListAsync(ct);
        var sourceByFollowup = new Dictionary<long, SourceInfo>();
        foreach (var f in followups)
        {
            if (!result.TryGetValue(new SourceKey(f.BusinessModuleId!.Value, f.BusinessRecordId!), out var info)) continue;
            info.Total++;
            if (f.CompletedAt is null) info.Pending++;
            sourceByFollowup[f.Id] = info;
        }
        if (sourceByFollowup.Count > 0)
        {
            var followupIds = sourceByFollowup.Keys.ToList();
            var escalationFollowupIds = await db.Escalations.AsNoTracking()
                .Where(e => !e.IsDeleted && e.ResolvedAt == null && e.FollowupId.HasValue && followupIds.Contains(e.FollowupId.Value))
                .Select(e => e.FollowupId!.Value).ToListAsync(ct);
            foreach (var id in escalationFollowupIds) sourceByFollowup[id].OpenEscalations++;
        }
        return result;
    }

    // ---------------- Task Register ----------------
    public async Task<PagedResult<EmReportTaskRowDto>> GetTasksAsync(EmReportTaskRegisterQueryDto query, CancellationToken ct)
    {
        // Validate everything before touching the database.
        string? status = null;
        if (!string.IsNullOrWhiteSpace(query.ExecutionStatus))
            status = RegisterStatuses.FirstOrDefault(x => string.Equals(x, query.ExecutionStatus.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new BadRequestException($"ExecutionStatus must be one of: {string.Join(", ", RegisterStatuses)}.");
        Performance? performanceFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Performance))
            performanceFilter = query.Performance.Trim().ToLowerInvariant() switch
            {
                "ontime" => Performance.OnTime, "delayed" => Performance.Delayed, "notmeasured" => Performance.NotMeasured,
                _ => throw new BadRequestException("Performance must be one of: OnTime, Delayed, NotMeasured.")
            };
        var term = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim().ToLower();
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : query.PageSize > 200 ? 200 : query.PageSize;

        // 1) date/module cohort + DB-side narrowing that does not depend on derived state
        //    (search on stable text fields; persisted status where it maps 1:1).
        var prepared = await PrepareAsync(query, ct, q =>
        {
            if (term is not null)
                q = q.Where(x => x.Task.ToLower().Contains(term) || x.BusinessRecordId.ToLower().Contains(term)
                    || x.BusinessModule.Name.ToLower().Contains(term));
            if (status is "InProgress" or "Paused") q = q.Where(x => x.ExecutionStatus == EaTaskExecutionStatus.InProgress);
            else if (status is not null) q = q.Where(x => x.ExecutionStatus == status);
            return q;
        });
        if (prepared.Count == 0) return new PagedResult<EmReportTaskRowDto> { PageNumber = page, PageSize = pageSize };

        // 2) derived-state filters on the prepared rows (paused is derived; performance is the shared classifier)
        IEnumerable<PreparedTask> rows = prepared;
        if (status == "Paused") rows = rows.Where(x => x.Bucket == Bucket.Paused);
        else if (status == "InProgress") rows = rows.Where(x => x.Bucket == Bucket.InProgress);
        if (performanceFilter.HasValue) rows = rows.Where(x => (x.Performance ?? Performance.NotMeasured) == performanceFilter.Value);
        var filtered = rows.ToList();

        var sources = await LoadSourceInfoAsync(filtered, ct);
        SourceInfo Info(PreparedTask t) => sources[new SourceKey(t.Task.BusinessModuleId, t.Task.BusinessRecordId)];
        if (query.RequiresFollowup.HasValue) filtered = filtered.Where(x => (Info(x).Pending > 0) == query.RequiresFollowup.Value).ToList();
        if (query.HasOpenEscalation.HasValue) filtered = filtered.Where(x => (Info(x).OpenEscalations > 0) == query.HasOpenEscalation.Value).ToList();

        // 3) deterministic order, filtered total, then the page
        var ordered = filtered.OrderByDescending(x => x.Task.CreatedDate).ThenByDescending(x => x.Task.Id).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(t =>
        {
            var info = Info(t);
            return new EmReportTaskRowDto
            {
                EaTaskId = t.Task.Id, BusinessModuleId = t.Task.BusinessModuleId,
                ModuleName = t.Task.BusinessModule?.Name ?? t.Task.ModuleName,
                BusinessRecordId = t.Task.BusinessRecordId, Task = t.Task.Task,
                ExecutionStatus = t.Task.ExecutionStatus, IsPaused = t.Bucket == Bucket.Paused,
                DueDate = t.Deadline,
                Performance = (t.Performance ?? Performance.NotMeasured).ToString(),
                FollowupStatus = info.Total == 0 ? "NoFollowup" : info.Pending > 0 ? "Pending" : "Completed",
                PendingFollowupCount = info.Pending, OpenEscalationCount = info.OpenEscalations,
                AllottedTatMinutes = t.Task.AllottedTatMinutes, CurrentOrFinalTatUsedMinutes = t.TatUsed
            };
        }).ToList();
        return new PagedResult<EmReportTaskRowDto> { Items = items, PageNumber = page, PageSize = pageSize, TotalCount = ordered.Count };
    }

    private static readonly string[] RegisterStatuses = ["NotStarted", "InProgress", "Paused", "Completed", "Cancelled"];

    private IQueryable<EaTask> BuildCohortQuery(EmReportOverviewQueryDto query)
    {
        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value.Date > query.ToDate.Value.Date)
            throw new BadRequestException("FromDate must be on or before ToDate.");
        if (query.BusinessModuleId.HasValue && query.BusinessModuleId.Value <= 0)
            throw new BadRequestException("BusinessModuleId must be positive.");
        var taskQuery = db.Tasks.AsNoTracking().Where(x => !x.IsDeleted);
        if (query.BusinessModuleId.HasValue) taskQuery = taskQuery.Where(x => x.BusinessModuleId == query.BusinessModuleId.Value);
        if (query.FromDate.HasValue) taskQuery = taskQuery.Where(x => x.CreatedDate >= IndiaDateStartUtc(query.FromDate.Value));
        if (query.ToDate.HasValue) taskQuery = taskQuery.Where(x => x.CreatedDate < IndiaDateStartUtc(query.ToDate.Value.Date.AddDays(1)));
        return taskQuery;
    }

    private static DateTime IndiaDateStartUtc(DateTime date) => DateTime.SpecifyKind(date.Date.AddHours(-5.5), DateTimeKind.Utc);

    private async Task<Dictionary<long, List<WorkPause>>> LoadPausesAsync(IReadOnlyCollection<EaTask> tasks, CancellationToken ct)
    {
        var workflowIds = tasks.Where(x => x.WorkflowInstanceId.HasValue).Select(x => x.WorkflowInstanceId!.Value).Distinct().ToList();
        if (workflowIds.Count == 0) return [];
        var pauses = await db.WorkPauses.AsNoTracking().Where(x => x.WorkflowInstanceId.HasValue && workflowIds.Contains(x.WorkflowInstanceId.Value) && !x.IsDeleted).ToListAsync(ct);
        return pauses.GroupBy(x => x.WorkflowInstanceId!.Value).ToDictionary(x => x.Key, x => x.ToList());
    }

    private async Task<DeadlineLookup> LoadDeadlinesAsync(IReadOnlyCollection<EaTask> tasks, CancellationToken ct)
    {
        var taskNames = tasks.Select(x => new { Task = x, Name = x.BusinessModule?.Name ?? x.ModuleName }).ToList();
        var approvalReferences = taskNames.Where(x => IsModule(x.Name, "EA Approval")).Select(x => x.Task.BusinessRecordId).Distinct().ToList();
        var travelIds = ParseIds(taskNames.Where(x => IsModule(x.Name, "Travel & Hospitality")).Select(x => x.Task.BusinessRecordId));
        var delegationIds = ParseIds(taskNames.Where(x => IsModule(x.Name, "Delegation")).Select(x => x.Task.BusinessRecordId));
        var approvals = approvalReferences.Count == 0 ? [] : await db.ApprovalRequests.AsNoTracking().Where(x => !x.IsDeleted && approvalReferences.Contains(x.ReferenceNo)).Select(x => new { x.ReferenceNo, x.RequiredApprovalDate }).ToListAsync(ct);
        var travel = travelIds.Count == 0 ? [] : await db.TravelRequests.AsNoTracking().Where(x => !x.IsDeleted && travelIds.Contains(x.Id)).Select(x => new { x.Id, x.RequiredDate }).ToListAsync(ct);
        var delegations = delegationIds.Count == 0 ? [] : await db.Delegations.AsNoTracking().Where(x => !x.IsDeleted && delegationIds.Contains(x.Id)).Select(x => new { x.Id, x.DueDate }).ToListAsync(ct);
        return new(approvals.ToDictionary(x => x.ReferenceNo, x => x.RequiredApprovalDate, StringComparer.Ordinal), travel.ToDictionary(x => x.Id, x => x.RequiredDate), delegations.ToDictionary(x => x.Id, x => x.DueDate));
    }

    private static List<long> ParseIds(IEnumerable<string> values) => values.Select(x => long.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? (long?)id : null).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();

    private static Performance ClassifyPerformance(EaTask task, IReadOnlyCollection<WorkPause> pauses, DeadlineLookup deadlines, DateTime now, DateTime today)
    {
        var module = task.BusinessModule?.Name ?? task.ModuleName;
        // Meeting always; Delegation only when a TAT snapshot exists. A TAT-enabled task is measured by TAT alone and
        // never also by its planned end date; Delegations without a snapshot keep the end-date rule below.
        if (IsModule(module, "Meeting") || (IsModule(module, "Delegation") && task.AllottedTatMinutes.HasValue))
        {
            var used = EaTaskService.CalculateCurrentTatUsedMinutes(task, pauses, now);
            return !task.AllottedTatMinutes.HasValue || !used.HasValue ? Performance.NotMeasured : used <= task.AllottedTatMinutes ? Performance.OnTime : Performance.Delayed;
        }
        DateTime? deadline = GetBusinessDeadline(task, deadlines);
        if (!deadline.HasValue) return Performance.NotMeasured;
        var observed = task.ExecutionStatus == EaTaskExecutionStatus.Completed ? task.CompletedAt.HasValue ? IndiaBusinessCalendar.ToIndiaDate(task.CompletedAt.Value) : (DateTime?)null : today;
        return !observed.HasValue ? Performance.NotMeasured : observed.Value > deadline.Value.Date ? Performance.Delayed : Performance.OnTime;
    }

    private static DateTime? GetBusinessDeadline(EaTask task, DeadlineLookup deadlines)
    {
        var module = task.BusinessModule?.Name ?? task.ModuleName;
        return IsModule(module, "EA Approval") ? deadlines.Approvals.GetValueOrDefault(task.BusinessRecordId)
            : IsModule(module, "Travel & Hospitality") && long.TryParse(task.BusinessRecordId, out var travelId) ? deadlines.Travel.GetValueOrDefault(travelId)
            : IsModule(module, "Delegation") && long.TryParse(task.BusinessRecordId, out var delegationId) ? deadlines.Delegations.GetValueOrDefault(delegationId) : null;
    }

    private static bool IsModule(string? name, string expected) => string.Equals(name?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    private sealed record DeadlineLookup(Dictionary<string, DateTime?> Approvals, Dictionary<long, DateTime?> Travel, Dictionary<long, DateTime?> Delegations);
    private sealed record SourceKey(long BusinessModuleId, string BusinessRecordId);
}
