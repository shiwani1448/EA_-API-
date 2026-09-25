using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public interface IEmEmployeeReportService
{
    Task<EmKpiResponseDto> GetKpisAsync(EmEmployeeReportQueryDto query, CancellationToken ct);
    Task<EmTrendResponseDto> GetTrendsAsync(EmEmployeeTrendQueryDto query, CancellationToken ct);
    Task<PagedResult<EmWorkItemDto>> GetWorkItemsAsync(EmWorkItemQueryDto query, CancellationToken ct);
}

/// <summary>
/// Read-only EM Employee Report. Every module's work is normalised into one list of work items:
/// Delegation and Approval phases (Actual / Review / Rework — all counted for the doer), Meetings
/// (one item per matching doer), Follow-ups and Travel requests. A work item belongs to the ISO
/// week of its planned date in India time, Monday–Saturday. Performance: completed within TAT or
/// by the due date = OnTime, completed late = Delayed, open and past TAT/due date = Overdue, open
/// and still within time = Pending, completed with neither TAT nor due date = NotMeasured.
/// Approval and Travel store only a display name for the EA who did the work, so they are matched
/// by name. Nothing is written.
/// </summary>
public sealed class EmEmployeeReportService(EaFmsDbContext db) : IEmEmployeeReportService
{
    public const string ModDelegation = "Delegation", ModApproval = "Approval", ModMeeting = "Meeting",
        ModFollowup = "Follow-up", ModTravel = "Travel";
    private const string TypeActual = "Actual", TypeReview = "Review", TypeRework = "Rework", TypeMeeting = "Meeting";
    private const string StNotStarted = "NotStarted", StInProgress = "InProgress", StPaused = "Paused",
        StCompleted = "Completed", StCancelled = "Cancelled";
    private const string PerfOnTime = "OnTime", PerfDelayed = "Delayed", PerfOverdue = "Overdue",
        PerfPending = "Pending", PerfNotMeasured = "NotMeasured";
    private static readonly string[] Modules = [ModDelegation, ModApproval, ModMeeting, ModFollowup, ModTravel];

    // =================================================================================== public API

    public async Task<EmKpiResponseDto> GetKpisAsync(EmEmployeeReportQueryDto query, CancellationToken ct)
    {
        var (year, week) = ResolveWeek(query.Year, query.Week);
        var (start, end) = WeekRange(year, week);
        var (prevYear, prevWeek) = Shift(year, week, -1);
        var items = await LoadAsync(query.EmployeeId, query.EmployeeName, ct);

        var current = items.Where(i => i.Year == year && i.Week == week && i.Status != StCancelled).ToList();
        var previous = items.Where(i => i.Year == prevYear && i.Week == prevWeek && i.Status != StCancelled).ToList();

        EmKpiCellDto Cell(Func<WorkItem, bool> pick)
        {
            var cell = BuildCell(current.Where(pick));
            var prev = BuildCell(previous.Where(pick));
            cell.Delta = new EmKpiDeltaDto
            {
                Planned = cell.Planned - prev.Planned, Completed = cell.Completed - prev.Completed,
                OnTime = cell.OnTime - prev.OnTime, Delayed = cell.Delayed - prev.Delayed
            };
            return cell;
        }

        var response = new EmKpiResponseDto
        {
            EmployeeId = Clean(query.EmployeeId), EmployeeName = Clean(query.EmployeeName),
            Year = year, Week = week, WeekStart = start, WeekEnd = end,
            Matrix = new EmKpiMatrixDto
            {
                Summary = Cell(_ => true),
                Actual = Cell(i => i.TaskType == TypeActual),
                Review = Cell(i => i.TaskType == TypeReview),
                Rework = Cell(i => i.TaskType == TypeRework),
                Meeting = Cell(i => i.TaskType == TypeMeeting),
            },
            Modules = Modules.Select(m => new EmKpiModuleDto { Module = m, Kpi = Cell(i => i.Module == m) }).ToList(),
            TaskStatus = BuildTaskStatus(current),
            Tat = BuildTat(current),
            CarryForwardOverdue = items.Count(i => i.Performance == PerfOverdue && i.PlannedIndiaDate.HasValue && i.PlannedIndiaDate.Value < start),
        };
        response.FocusAreas = BuildFocusAreas(response);
        return response;
    }

    public async Task<EmTrendResponseDto> GetTrendsAsync(EmEmployeeTrendQueryDto query, CancellationToken ct)
    {
        if (query.Weeks is < 1 or > 12) throw new BadRequestException("Weeks must be between 1 and 12.");
        var (year, week) = ResolveWeek(query.Year, query.Week);
        var items = await LoadAsync(query.EmployeeId, query.EmployeeName, ct);
        var response = new EmTrendResponseDto { EmployeeId = Clean(query.EmployeeId), EmployeeName = Clean(query.EmployeeName) };
        for (var offset = query.Weeks - 1; offset >= 0; offset--)
        {
            var (y, w) = Shift(year, week, -offset);
            var (start, end) = WeekRange(y, w);
            var inWeek = items.Where(i => i.Year == y && i.Week == w && i.Status != StCancelled).ToList();
            response.Weeks.Add(new EmTrendWeekDto
            {
                Year = y, Week = w, Label = $"W{w}", WeekStart = start, WeekEnd = end,
                Summary = Point(inWeek),
                Actual = Point(inWeek.Where(i => i.TaskType == TypeActual)),
                Review = Point(inWeek.Where(i => i.TaskType == TypeReview)),
                Rework = Point(inWeek.Where(i => i.TaskType == TypeRework)),
                Meeting = Point(inWeek.Where(i => i.TaskType == TypeMeeting)),
            });
        }
        return response;
    }

    public async Task<PagedResult<EmWorkItemDto>> GetWorkItemsAsync(EmWorkItemQueryDto query, CancellationToken ct)
    {
        if (query.Page < 1) throw new BadRequestException("Page must be 1 or greater.");
        if (query.PageSize is < 1 or > 200) throw new BadRequestException("PageSize must be between 1 and 200.");
        var module = Allowed(query.Module, Modules, "Module");
        var taskType = Allowed(query.TaskType, [TypeActual, TypeReview, TypeRework, TypeMeeting], "TaskType");
        var status = Allowed(query.Status, [StNotStarted, StInProgress, StPaused, StCompleted, StCancelled], "Status");
        var performance = Allowed(query.Performance, [PerfOnTime, PerfDelayed, PerfOverdue, PerfPending, PerfNotMeasured], "Performance");

        IEnumerable<WorkItem> items = await LoadAsync(query.EmployeeId, query.EmployeeName, ct);
        if (!query.AllWeeks)
        {
            var (year, week) = ResolveWeek(query.Year, query.Week);
            items = items.Where(i => i.Year == year && i.Week == week);
        }
        if (module is not null) items = items.Where(i => i.Module == module);
        if (taskType is not null) items = items.Where(i => i.TaskType == taskType);
        if (status is not null) items = items.Where(i => i.Status == status);
        if (performance is not null) items = items.Where(i => i.Performance == performance);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            items = items.Where(i => Contains(i.Title, term) || Contains(i.ReferenceNo, term) || Contains(i.OwnerName, term));
        }

        var ordered = items.OrderByDescending(i => i.PlannedDate ?? DateTime.MinValue).ThenBy(i => i.ItemKey, StringComparer.Ordinal).ToList();
        return new PagedResult<EmWorkItemDto>
        {
            PageNumber = query.Page, PageSize = query.PageSize, TotalCount = ordered.Count,
            Items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(ToDto).ToList(),
        };
    }

    // =================================================================================== loading

    private sealed class WorkItem
    {
        public string ItemKey = "", Module = "", TaskType = "", Status = "", Performance = "";
        public int ReviewCycleNumber;
        public long RecordId;
        public long? EaTaskId;
        public string? ReferenceNo, Title, OwnerId, OwnerName;
        public DateTime? PlannedDate, PlannedIndiaDate, DueDate, StartedAt, CompletedAt;
        public int? Year, Week, AllottedTatMinutes, TatUsedMinutes;
        public bool IsPaused;
    }

    /// <summary>Loads and normalises every module's work for the employee (all weeks).</summary>
    private async Task<List<WorkItem>> LoadAsync(string? employeeId, string? employeeName, CancellationToken ct)
    {
        var id = Clean(employeeId);
        var name = NormName(employeeName);
        var now = Clock.UtcNowTz;
        var today = IndiaBusinessCalendar.ToIndiaDate(now);

        // ---- source rows (small, per-employee sets; matching is done in memory so id and name rules stay identical)
        var delegations = (await db.Delegations.AsNoTracking().Where(d => !d.IsDeleted).ToListAsync(ct))
            .Where(d => MatchesById(id, name, d.DoerId, d.DoerNameSnapshot)).ToList();
        var approvals = (await db.ApprovalRequests.AsNoTracking().Where(a => !a.IsDeleted).ToListAsync(ct))
            .Where(a => MatchesByName(id, name, a.RequestedBy ?? a.CreatedBy)).ToList();
        var meetings = await db.Meetings.AsNoTracking().Where(m => !m.IsDeleted).ToListAsync(ct);
        var followups = (await db.Followups.AsNoTracking().Where(f => !f.IsDeleted && f.EaTaskId != null).ToListAsync(ct))
            .Where(f => MatchesById(id, name, f.DoerId, f.DoerName)).ToList();
        var travel = (await db.TravelRequests.AsNoTracking().Where(t => !t.IsDeleted).ToListAsync(ct))
            .Where(t => MatchesByName(id, name, t.CreatedBy)).ToList();

        var delegationIds = delegations.Select(d => d.Id).ToList();
        var approvalIds = approvals.Select(a => a.Id).ToList();
        var delegationPhases = delegationIds.Count == 0 ? [] : await db.DelegationPhaseTats.AsNoTracking()
            .Where(p => delegationIds.Contains(p.DelegationId)).ToListAsync(ct);
        var approvalPhases = approvalIds.Count == 0 ? [] : await db.ApprovalPhaseTats.AsNoTracking()
            .Where(p => approvalIds.Contains(p.ApprovalRequestId)).ToListAsync(ct);

        var meetingIdTexts = meetings.Select(m => m.Id.ToString(CultureInfo.InvariantCulture)).ToList();
        var taskIds = delegations.Select(d => d.EaTaskId).Concat(approvals.Select(a => a.EaTaskId))
            .Concat(followups.Select(f => f.EaTaskId!.Value)).Concat(travel.Select(t => t.EaTaskId)).Distinct().ToList();
        var tasks = await db.Tasks.AsNoTracking()
            .Where(t => !t.IsDeleted && (taskIds.Contains(t.Id) || (t.ModuleName == ModMeeting && meetingIdTexts.Contains(t.BusinessRecordId))))
            .ToListAsync(ct);
        var taskById = tasks.ToDictionary(t => t.Id);
        var meetingTaskByRecord = tasks.Where(t => t.ModuleName == ModMeeting)
            .GroupBy(t => t.BusinessRecordId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Id).First());

        var workflowIds = tasks.Where(t => t.WorkflowInstanceId.HasValue).Select(t => t.WorkflowInstanceId!.Value)
            .Concat(meetings.Where(m => m.WorkflowInstanceId.HasValue).Select(m => m.WorkflowInstanceId!.Value)).Distinct().ToList();
        var pausesByWorkflow = workflowIds.Count == 0 ? new Dictionary<long, List<WorkPause>>()
            : (await db.WorkPauses.AsNoTracking().Where(p => p.WorkflowInstanceId.HasValue && workflowIds.Contains(p.WorkflowInstanceId.Value) && !p.IsDeleted)
                .ToListAsync(ct)).GroupBy(p => p.WorkflowInstanceId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        IReadOnlyCollection<WorkPause> Pauses(long? workflowId) =>
            workflowId.HasValue && pausesByWorkflow.TryGetValue(workflowId.Value, out var list) ? list : [];

        var items = new List<WorkItem>();

        // ---- Delegation: one item per phase (Actual / Review n / Rework n); all counted for the doer
        var phasesByDelegation = delegationPhases.GroupBy(p => p.DelegationId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var d in delegations)
        {
            taskById.TryGetValue(d.EaTaskId, out var task);
            var pauses = Pauses(task?.WorkflowInstanceId);
            var phases = phasesByDelegation.GetValueOrDefault(d.Id) ?? [];
            if (phases.Count == 0)
            {
                // Not started yet (the Actual phase row is created on Start).
                var completed = d.Status == DelegationStatus.Completed;
                items.Add(NewItem(ModDelegation, TypeActual, 0, d.Id, d.EaTaskId, d.ReferenceNo, d.Title, d.DoerId, d.DoerNameSnapshot,
                    planned: d.DueDate ?? d.CreatedDate, due: d.DueDate, started: d.StartedAt, completedAt: completed ? d.CompletedAt : null,
                    status: completed ? StCompleted : StNotStarted, isPaused: false,
                    allotted: task?.AllottedTatMinutes, used: completed ? task?.TatUsedMinutes : null, now, today));
                continue;
            }
            foreach (var p in phases.OrderBy(p => p.ReviewCycleNumber).ThenBy(p => p.Id))
            {
                var (status, paused, used) = PhaseState(p.StartedAt, p.EndedAt, p.AllottedTatMinutes, p.TatUsedMinutes, pauses, now);
                var isActual = p.TaskType == TypeActual;
                items.Add(NewItem(ModDelegation, p.TaskType, p.ReviewCycleNumber, d.Id, d.EaTaskId, d.ReferenceNo, d.Title, d.DoerId, d.DoerNameSnapshot,
                    planned: isActual ? d.DueDate ?? p.StartedAt ?? p.CreatedDate : p.StartedAt ?? p.CreatedDate,
                    due: isActual ? d.DueDate : null, started: p.StartedAt, completedAt: p.EndedAt,
                    status, paused, p.AllottedTatMinutes, used, now, today));
            }
        }

        // ---- Approval: one item per phase; owner is the EA who raised it (matched by name)
        var phasesByApproval = approvalPhases.GroupBy(p => p.ApprovalRequestId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var a in approvals)
        {
            taskById.TryGetValue(a.EaTaskId, out var task);
            var pauses = Pauses(task?.WorkflowInstanceId);
            var ownerName = a.RequestedBy ?? a.CreatedBy;
            var rejected = string.Equals(a.WorkflowStatus, "Rejected", StringComparison.OrdinalIgnoreCase);
            var phases = phasesByApproval.GetValueOrDefault(a.Id) ?? [];
            if (phases.Count == 0)
            {
                var approved = string.Equals(a.WorkflowStatus, "Approved", StringComparison.OrdinalIgnoreCase);
                items.Add(NewItem(ModApproval, TypeActual, 0, a.Id, a.EaTaskId, a.ReferenceNo, a.RequestTitle, null, ownerName,
                    planned: a.RequiredApprovalDate ?? a.CreatedAt, due: a.RequiredApprovalDate, started: a.SubmittedAt,
                    completedAt: approved ? a.ApprovedAt : null, status: approved ? StCompleted : rejected ? StCancelled : StNotStarted,
                    isPaused: false, allotted: task?.AllottedTatMinutes, used: approved ? task?.TatUsedMinutes : null, now, today));
                continue;
            }
            foreach (var p in phases.OrderBy(p => p.ReviewCycleNumber).ThenBy(p => p.Id))
            {
                var (status, paused, used) = PhaseState(p.StartedAt, p.EndedAt, p.AllottedTatMinutes, p.TatUsedMinutes, pauses, now);
                if (rejected && p.EndedAt is null) (status, paused) = (StCancelled, false);
                var isActual = p.TaskType == TypeActual;
                items.Add(NewItem(ModApproval, p.TaskType, p.ReviewCycleNumber, a.Id, a.EaTaskId, a.ReferenceNo, a.RequestTitle, null, ownerName,
                    planned: isActual ? a.RequiredApprovalDate ?? p.StartedAt ?? p.CreatedDate : p.StartedAt ?? p.CreatedDate,
                    due: isActual ? a.RequiredApprovalDate : null, started: p.StartedAt, completedAt: p.EndedAt,
                    status, paused, p.AllottedTatMinutes, used, now, today));
            }
        }

        // ---- Meeting: one item per matching doer (whole-team view: one item per meeting)
        foreach (var m in meetings)
        {
            var owners = MeetingOwners(m, id, name);
            if (owners.Count == 0) continue;
            meetingTaskByRecord.TryGetValue(m.Id.ToString(CultureInfo.InvariantCulture), out var task);
            var (status, paused, used) = TaskState(task, Pauses(task?.WorkflowInstanceId ?? m.WorkflowInstanceId), now, fallbackCompletedAt: m.CompletedAt);
            var meetingDate = m.StartDateTime ?? m.MeetingDate;
            foreach (var (ownerId, ownerName) in owners)
                items.Add(NewItem(ModMeeting, TypeMeeting, 0, m.Id, task?.Id, m.MeetingNumber, m.Title, ownerId, ownerName,
                    planned: meetingDate ?? m.CreatedDate, due: meetingDate, started: task?.StartedAt, completedAt: task?.CompletedAt ?? m.CompletedAt,
                    status, paused, task?.AllottedTatMinutes, used, now, today, keySuffix: ownerId));
        }

        // ---- Follow-up: its own Actual execution task
        foreach (var f in followups)
        {
            taskById.TryGetValue(f.EaTaskId!.Value, out var task);
            var (status, paused, used) = TaskState(task, Pauses(task?.WorkflowInstanceId), now, fallbackCompletedAt: f.CompletedAt);
            items.Add(NewItem(ModFollowup, TypeActual, 0, f.Id, f.EaTaskId, null, f.Subject, f.DoerId, f.DoerName,
                planned: f.DueAt, due: f.DueAt, started: task?.StartedAt, completedAt: task?.CompletedAt ?? f.CompletedAt,
                status, paused, task?.AllottedTatMinutes, used, now, today));
        }

        // ---- Travel: one item per request (owner matched by name)
        foreach (var t in travel)
        {
            taskById.TryGetValue(t.EaTaskId, out var task);
            var (status, paused, used) = TaskState(task, Pauses(task?.WorkflowInstanceId), now, fallbackCompletedAt: t.CompletedAt);
            items.Add(NewItem(ModTravel, TypeActual, 0, t.Id, t.EaTaskId, t.ReferenceNo, t.Purpose ?? $"Travel {t.ReferenceNo}", null, t.CreatedBy,
                planned: t.RequiredDate ?? t.CreatedDate, due: t.RequiredDate, started: task?.StartedAt ?? t.StartedAt,
                completedAt: task?.CompletedAt ?? t.CompletedAt, status, paused, task?.AllottedTatMinutes, used, now, today));
        }

        return items;
    }

    private static WorkItem NewItem(string module, string taskType, int cycle, long recordId, long? eaTaskId, string? reference, string? title,
        string? ownerId, string? ownerName, DateTime? planned, DateTime? due, DateTime? started, DateTime? completedAt,
        string status, bool isPaused, int? allotted, int? used, DateTime now, DateTime today, string? keySuffix = null)
    {
        var plannedIndia = planned.HasValue ? IndiaBusinessCalendar.ToIndiaDate(planned.Value) : (DateTime?)null;
        var (year, week) = plannedIndia.HasValue ? WeekOf(plannedIndia.Value) : (null, null);
        var item = new WorkItem
        {
            ItemKey = $"{module}:{recordId}:{taskType}:{cycle}" + (keySuffix is null ? "" : $":{keySuffix}"),
            Module = module, TaskType = taskType, ReviewCycleNumber = cycle, RecordId = recordId, EaTaskId = eaTaskId,
            ReferenceNo = reference, Title = title, OwnerId = ownerId, OwnerName = ownerName,
            PlannedDate = planned, PlannedIndiaDate = plannedIndia, Year = year, Week = week,
            DueDate = due, StartedAt = started, CompletedAt = status == StCompleted ? completedAt : null,
            Status = status, IsPaused = isPaused, AllottedTatMinutes = allotted, TatUsedMinutes = used,
        };
        item.Performance = Classify(item, today);
        return item;
    }

    /// <summary>Phase row state: frozen values once ended, live TAT (pauses excluded) while open.</summary>
    private static (string Status, bool Paused, int? Used) PhaseState(DateTime? startedAt, DateTime? endedAt, int? allotted, int? frozenUsed,
        IReadOnlyCollection<WorkPause> pauses, DateTime now)
    {
        if (endedAt.HasValue) return (StCompleted, false, frozenUsed);
        if (!startedAt.HasValue) return (StNotStarted, false, null);
        var relevant = pauses.Where(p => WorkPauseClassifier.IsSimplePause(p) && p.StartAt < now && (p.EndAt ?? DateTime.MaxValue) > startedAt.Value).ToList();
        var paused = relevant.Any(p => p.EndAt is null);
        int? used = allotted.HasValue ? (int)TatSummaryCalculator.Calculate(allotted.Value, startedAt, null, relevant, now).Tat!.Value.TotalMinutes : null;
        return (paused ? StPaused : StInProgress, paused, used);
    }

    /// <summary>EaTask-based state (Meeting, Follow-up, Travel) using the shared TAT calculation.</summary>
    private static (string Status, bool Paused, int? Used) TaskState(EaTask? task, IReadOnlyCollection<WorkPause> pauses, DateTime now, DateTime? fallbackCompletedAt)
    {
        if (task is null) return (fallbackCompletedAt.HasValue ? StCompleted : StNotStarted, false, null);
        var paused = task.ExecutionStatus == EaTaskExecutionStatus.InProgress && pauses.Any(p => p.EndAt is null && WorkPauseClassifier.IsSimplePause(p));
        var status = task.ExecutionStatus switch
        {
            EaTaskExecutionStatus.Completed => StCompleted,
            EaTaskExecutionStatus.Cancelled => StCancelled,
            EaTaskExecutionStatus.InProgress => paused ? StPaused : StInProgress,
            _ => StNotStarted
        };
        var used = status == StCancelled ? null : EaTaskService.CalculateCurrentTatUsedMinutes(task, pauses, now);
        return (status, paused, used);
    }

    /// <summary>TAT wins when present; otherwise the business due date; otherwise not measured.</summary>
    private static string Classify(WorkItem i, DateTime today)
    {
        if (i.Status == StCancelled) return PerfNotMeasured;
        var hasTat = i.AllottedTatMinutes.HasValue && i.TatUsedMinutes.HasValue;
        var dueDay = i.DueDate.HasValue ? IndiaBusinessCalendar.ToIndiaDate(i.DueDate.Value) : (DateTime?)null;
        if (i.Status == StCompleted)
        {
            if (hasTat) return i.TatUsedMinutes <= i.AllottedTatMinutes ? PerfOnTime : PerfDelayed;
            if (dueDay.HasValue && i.CompletedAt.HasValue)
                return IndiaBusinessCalendar.ToIndiaDate(i.CompletedAt.Value) <= dueDay.Value ? PerfOnTime : PerfDelayed;
            return PerfNotMeasured;
        }
        if (hasTat && i.TatUsedMinutes > i.AllottedTatMinutes) return PerfOverdue;
        if (dueDay.HasValue && today > dueDay.Value) return PerfOverdue;
        return PerfPending;
    }

    // =================================================================================== KPI math

    private static EmKpiCellDto BuildCell(IEnumerable<WorkItem> source)
    {
        var items = source.ToList();
        var cell = new EmKpiCellDto
        {
            Planned = items.Count,
            Completed = items.Count(i => i.Status == StCompleted),
            OnTime = items.Count(i => i.Performance == PerfOnTime),
            Delayed = items.Count(i => i.Performance == PerfDelayed),
            NotMeasured = items.Count(i => i.Status == StCompleted && i.Performance == PerfNotMeasured),
            Overdue = items.Count(i => i.Performance == PerfOverdue),
            Pending = items.Count(i => i.Performance == PerfPending),
        };
        cell.NotCompleted = cell.Planned - cell.Completed;
        cell.NotCompletedPct = Pct(cell.NotCompleted, cell.Planned);
        cell.DelayedPct = Pct(cell.Delayed, cell.Completed);
        return cell;
    }

    private static EmTrendPointDto Point(IEnumerable<WorkItem> source)
    {
        var items = source.ToList();
        return new EmTrendPointDto
        {
            Planned = items.Count, Completed = items.Count(i => i.Status == StCompleted),
            OnTime = items.Count(i => i.Performance == PerfOnTime), Delayed = items.Count(i => i.Performance == PerfDelayed),
        };
    }

    private static EmTaskStatusDto BuildTaskStatus(IReadOnlyCollection<WorkItem> items)
    {
        var nonRework = items.Where(i => i.TaskType != TypeRework).ToList();
        return new EmTaskStatusDto
        {
            Total = items.Count,
            Rework = items.Count(i => i.TaskType == TypeRework),
            OnTime = nonRework.Count(i => i.Performance == PerfOnTime),
            Delayed = nonRework.Count(i => i.Performance == PerfDelayed),
            Overdue = nonRework.Count(i => i.Performance == PerfOverdue),
            Pending = nonRework.Count(i => i.Performance == PerfPending),
            NotMeasured = nonRework.Count(i => i.Performance == PerfNotMeasured),
        };
    }

    private static EmTatDto BuildTat(IReadOnlyCollection<WorkItem> items)
    {
        var measured = items.Where(i => i.AllottedTatMinutes.HasValue && i.TatUsedMinutes.HasValue).ToList();
        var allotted = measured.Sum(i => i.AllottedTatMinutes!.Value);
        var used = measured.Sum(i => i.TatUsedMinutes!.Value);
        return new EmTatDto { MeasuredItems = measured.Count, AllottedMinutes = allotted, UsedMinutes = used, DifferenceMinutes = allotted - used, IsOver = used > allotted };
    }

    /// <summary>Fixed rules on the KPIs — no AI.</summary>
    private static EmFocusAreasDto BuildFocusAreas(EmKpiResponseDto r)
    {
        var s = r.Matrix.Summary;
        var focus = new EmFocusAreasDto();
        if (s.Planned == 0) return focus;
        var completionPct = 100m - s.NotCompletedPct;
        if (completionPct >= 90) focus.Strengths.Add("High completion rate");
        if (s.Completed > 0 && s.DelayedPct <= 20) focus.Strengths.Add("Mostly completed on time");
        if (r.Matrix.Rework.Planned == 0 && s.Completed > 0) focus.Strengths.Add("No rework this week");
        if (r.Tat.MeasuredItems > 0 && !r.Tat.IsOver) focus.Strengths.Add("Within the allotted TAT");
        if (s.Delayed > 0) focus.Improve.Add($"Reduce delays ({s.Delayed} delayed)");
        if (s.Overdue > 0) focus.Improve.Add($"Clear overdue work ({s.Overdue} overdue)");
        if (s.Pending > 0) focus.Improve.Add($"Complete the remaining planned work ({s.Pending} still open)");
        if (r.Matrix.Rework.Planned > 0) focus.Improve.Add($"Reduce rework ({r.Matrix.Rework.Planned} rework tasks)");
        if (r.Tat.IsOver) focus.Improve.Add("TAT used is over the allotted limit");
        if (r.CarryForwardOverdue > 0) focus.Improve.Add($"Close {r.CarryForwardOverdue} overdue task(s) from earlier weeks");
        return focus;
    }

    // =================================================================================== weeks

    /// <summary>ISO week of an India date. Sunday belongs to no week (weeks run Monday–Saturday).</summary>
    private static (int? Year, int? Week) WeekOf(DateTime indiaDate) =>
        indiaDate.DayOfWeek == DayOfWeek.Sunday ? (null, null) : (ISOWeek.GetYear(indiaDate), ISOWeek.GetWeekOfYear(indiaDate));

    private static (DateTime Start, DateTime End) WeekRange(int year, int week)
    {
        var monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
        return (monday, monday.AddDays(5));
    }

    private static (int Year, int Week) Shift(int year, int week, int weeks)
    {
        var monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday).AddDays(7 * weeks);
        return (ISOWeek.GetYear(monday), ISOWeek.GetWeekOfYear(monday));
    }

    private static (int Year, int Week) ResolveWeek(int? year, int? week)
    {
        if (year is null && week is null)
        {
            var today = IndiaBusinessCalendar.ToIndiaDate(Clock.UtcNowTz);
            return (ISOWeek.GetYear(today), ISOWeek.GetWeekOfYear(today));
        }
        if (year is null || week is null) throw new BadRequestException("Year and Week must be sent together.");
        if (year is < 2000 or > 2100) throw new BadRequestException("Year must be between 2000 and 2100.");
        if (week < 1 || week > ISOWeek.GetWeeksInYear(year.Value))
            throw new BadRequestException($"Week must be between 1 and {ISOWeek.GetWeeksInYear(year.Value)} for {year}.");
        return (year.Value, week.Value);
    }

    // =================================================================================== matching / helpers

    /// <summary>Id-based modules: the employee id wins; with no id, match by name; with neither, include everything.</summary>
    private static bool MatchesById(string? id, string? name, string? candidateId, string? candidateName)
    {
        if (id is not null) return string.Equals(candidateId?.Trim(), id, StringComparison.OrdinalIgnoreCase);
        if (name is not null) return NormName(candidateName) == name;
        return true;
    }

    /// <summary>Name-only modules (Approval, Travel): the stored display name is compared with the name (or, with only an id, with the id).</summary>
    private static bool MatchesByName(string? id, string? name, string? candidateName)
    {
        if (name is not null) return NormName(candidateName) == name;
        if (id is not null) return string.Equals(candidateName?.Trim(), id, StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static List<(string? Id, string? Name)> MeetingOwners(Meeting m, string? id, string? name)
    {
        var owners = new List<(string? Id, string? Name)>();
        if (id is null && name is null)
        {
            owners.Add((null, m.DoerNames.Length == 0 ? null : string.Join(", ", m.DoerNames)));
            return owners;
        }
        for (var i = 0; i < m.DoerIds.Length; i++)
        {
            var doerName = i < m.DoerNames.Length ? m.DoerNames[i] : null;
            if (MatchesById(id, name, m.DoerIds[i], doerName)) owners.Add((m.DoerIds[i], doerName));
        }
        return owners;
    }

    private static EmWorkItemDto ToDto(WorkItem i) => new()
    {
        ItemKey = i.ItemKey, Module = i.Module, TaskType = i.TaskType, ReviewCycleNumber = i.ReviewCycleNumber,
        RecordId = i.RecordId, EaTaskId = i.EaTaskId, ReferenceNo = i.ReferenceNo, Title = i.Title,
        OwnerId = i.OwnerId, OwnerName = i.OwnerName, PlannedDate = i.PlannedDate, Year = i.Year, Week = i.Week,
        DueDate = i.DueDate, StartedAt = i.StartedAt, CompletedAt = i.CompletedAt, Status = i.Status, IsPaused = i.IsPaused,
        Performance = i.Performance, AllottedTatMinutes = i.AllottedTatMinutes, TatUsedMinutes = i.TatUsedMinutes,
        TatDifferenceMinutes = i.AllottedTatMinutes.HasValue && i.TatUsedMinutes.HasValue ? i.AllottedTatMinutes - i.TatUsedMinutes : null,
    };

    private static string? Allowed(string? value, string[] allowed, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return allowed.FirstOrDefault(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BadRequestException($"{field} must be one of: {string.Join(", ", allowed)}.");
    }

    private static decimal Pct(int part, int whole) => whole == 0 ? 0 : decimal.Round(part * 100m / whole, 2);
    private static bool Contains(string? text, string term) => text?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
