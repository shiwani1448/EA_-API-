using System.Globalization;
using FluentValidation;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class EaTaskService(EaFmsDbContext db, IEaTaskRepository repository, ITatRuleRepository rules,
    IValidator<CreateEaTaskDto> validator, ICurrentUserService user, IAuditService audit) : IEaTaskService
{
    // Explicit backend module policy for the no-TAT task-creation path. The frontend never
    // selects this; only module identity (resolved server-side by name) does. Follow-up uses
    // this as its EaTask creation entry point too: it always creates without a hard-required
    // TAT rule, then FollowupService itself does the soft (never-throwing) type-only rule
    // lookup and writes AllottedTatMinutes/TatRuleId onto the same EaTask afterward — see
    // FollowupService.CreateAsync for why a "no rule = no TAT" outcome must never throw here.
    private static readonly HashSet<string> NoTatAuthorizedModules =
        new(StringComparer.OrdinalIgnoreCase) { "EA Approval", "Travel & Hospitality", "Delegation", "Follow-up" };

    public static bool IsTypeOnlyTatModule(string moduleName) =>
        TypeOnlyTatModules.Contains(moduleName.Trim());

    public static bool IsNoTatAuthorized(string moduleName) =>
        NoTatAuthorizedModules.Contains(moduleName.Trim());

    public async Task<List<EaTaskResponseDto>> QueryAsync(long? moduleId, string? recordId, CancellationToken ct)
    {
        var query = repository.Query();
        if (moduleId.HasValue) query = query.Where(x => x.BusinessModuleId == moduleId.Value);
        if (recordId is not null) query = query.Where(x => x.BusinessRecordId == recordId.Trim());
        var tasks = await query.OrderByDescending(x => x.Id).ToListAsync(ct);
        var pausesByWorkflow = await LoadPausesAsync(tasks, ct);
        var now = Clock.UtcNowTz;
        return tasks.Select(t => ToDto(t, pausesByWorkflow, now)).ToList();
    }

    public async Task<PagedResult<EaTaskResponseDto>> QueryWorkspaceAsync(EaTaskWorkspaceQueryDto query, CancellationToken ct)
    {
        // Same visibility rule as every other central-task read (repository: not deleted).
        var q = repository.Query();
        if (query.BusinessModuleId.HasValue) q = q.Where(x => x.BusinessModuleId == query.BusinessModuleId.Value);
        if (!string.IsNullOrWhiteSpace(query.BusinessRecordId))
        {
            var recordId = query.BusinessRecordId.Trim();
            q = q.Where(x => x.BusinessRecordId == recordId);
        }
        if (!string.IsNullOrWhiteSpace(query.ExecutionStatus))
        {
            var status = EaTaskExecutionStatus.Canonicalize(query.ExecutionStatus)
                ?? throw new BadRequestException(
                    $"ExecutionStatus must be one of: {string.Join(", ", EaTaskExecutionStatus.Values)}.");
            q = q.Where(x => x.ExecutionStatus == status);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(x => x.Task.ToLower().Contains(term)
                || (x.Description != null && x.Description.ToLower().Contains(term))
                || x.ModuleName.ToLower().Contains(term)
                || x.BusinessRecordId.ToLower().Contains(term));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : query.PageSize > 200 ? 200 : query.PageSize;

        var offset = ((long)page - 1) * pageSize;
        if (offset > int.MaxValue) throw new BadRequestException("Page offset is too large.");

        var totalCount = await q.CountAsync(ct);
        // Newest central task first; Id breaks ties so paging is stable.
        var tasks = await q.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id)
            .Skip((int)offset).Take(pageSize).ToListAsync(ct);
        // One pause query for the whole page (no per-task lookups).
        var pausesByWorkflow = await LoadPausesAsync(tasks, ct);
        var now = Clock.UtcNowTz;
        return new PagedResult<EaTaskResponseDto>
        {
            Items = tasks.Select(t => ToDto(t, pausesByWorkflow, now)).ToList(),
            PageNumber = page, PageSize = pageSize, TotalCount = totalCount
        };
    }

    public async Task<EaTaskResponseDto> GetAsync(long id, CancellationToken ct)
    {
        var task = await repository.Query().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"EA task {id} not found.");
        var pausesByWorkflow = await LoadPausesAsync(new[] { task }, ct);
        return ToDto(task, pausesByWorkflow, Clock.UtcNowTz);
    }

    private async Task<Dictionary<long, List<WorkPause>>> LoadPausesAsync(IReadOnlyCollection<EaTask> tasks, CancellationToken ct)
    {
        var workflowIds = tasks.Where(t => t.WorkflowInstanceId.HasValue).Select(t => t.WorkflowInstanceId!.Value).Distinct().ToList();
        if (workflowIds.Count == 0) return new Dictionary<long, List<WorkPause>>();
        var pauses = await db.WorkPauses.AsNoTracking()
            .Where(p => p.WorkflowInstanceId.HasValue && workflowIds.Contains(p.WorkflowInstanceId.Value) && !p.IsDeleted)
            .ToListAsync(ct);
        return pauses.GroupBy(p => p.WorkflowInstanceId!.Value).ToDictionary(g => g.Key, g => g.ToList());
    }

    // How TAT is resolved at creation. Required = exact module + type + subtype (Meeting, generic endpoint);
    // None = no TAT (backend-only); TypeOnly = module + type, no subtype (backend-only, Delegation).
    private enum TatMode { Required, None, TypeOnly }

    // Delegation and Follow-up each have a single business classification (delegationType /
    // Followup.Type) and therefore no subtype — both resolve TAT rules as module + Type + TaskType only.
    private static readonly HashSet<string> TypeOnlyTatModules = new(StringComparer.OrdinalIgnoreCase) { "Delegation", "Follow-up" };

    public Task<EaTaskResponseDto> CreateAsync(CreateEaTaskDto dto, CancellationToken ct) =>
        CreateCoreAsync(dto, TatMode.Required, ct);

    // Backend-only Approval path; no public request can select this behavior.
    public Task<EaTaskResponseDto> CreateWithoutTatAsync(CreateEaTaskDto dto, CancellationToken ct) =>
        CreateCoreAsync(dto, TatMode.None, ct);

    // Backend-only Delegation path: TAT rule identity is module + Type (= delegationType), Subtype not applicable.
    public Task<EaTaskResponseDto> CreateWithTypeOnlyTatAsync(CreateEaTaskDto dto, CancellationToken ct) =>
        CreateCoreAsync(dto, TatMode.TypeOnly, ct);

    private async Task<EaTaskResponseDto> CreateCoreAsync(CreateEaTaskDto dto, TatMode mode, CancellationToken ct)
    {
        var requireTat = mode == TatMode.Required;
        dto.BusinessRecordId = dto.BusinessRecordId?.Trim()!;
        dto.Task = dto.Task?.Trim()!;
        dto.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid) throw new BusinessRuleException(string.Join("; ", validation.Errors.Select(x => x.ErrorMessage)));

        // Join an ambient transaction when Meeting (or another caller) already owns one.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        // Share locks keep the selected module, rule and optional workflow stable until insertion commits.
        var modules = await db.BusinessModules.FromSqlInterpolated(
            $"SELECT * FROM public.ea_business_modules WHERE \"Id\" = {dto.ModuleId} FOR SHARE").ToListAsync(ct);
        var module = modules.SingleOrDefault();
        if (module is null || !module.IsActive || module.IsDeleted)
            throw new BusinessRuleException("Module must exist and be active/non-deleted.");

        // Serialize creates for the same module/record, including the generic task endpoint.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({dto.ModuleId.ToString(CultureInfo.InvariantCulture) + ":" + dto.BusinessRecordId}, 0))", ct);
        if (await db.Tasks.AnyAsync(x => x.BusinessModuleId == dto.ModuleId && x.BusinessRecordId == dto.BusinessRecordId && !x.IsDeleted, ct))
            throw new BusinessRuleException("An EA task already exists for this business record.");
        var type = dto.Type;
        var subtype = dto.Subtype;
        var isApproval = string.Equals(module.Name.Trim(), "EA Approval", StringComparison.OrdinalIgnoreCase);
        if (mode == TatMode.None && !IsNoTatAuthorized(module.Name))
            throw new BusinessRuleException("Task creation without TAT is only supported for EA Approval, Travel & Hospitality, Delegation, and Follow-up.");
        if (mode == TatMode.TypeOnly && !TypeOnlyTatModules.Contains(module.Name.Trim()))
            throw new BusinessRuleException("Type-only TAT resolution is only supported for Delegation and Follow-up.");
        if (string.Equals(module.Name.Trim(), "Meeting", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(dto.BusinessRecordId, NumberStyles.None, CultureInfo.InvariantCulture, out var meetingId))
                throw new BusinessRuleException("Meeting business record ID is invalid.");
            var meeting = await db.Meetings.SingleOrDefaultAsync(x => x.Id == meetingId && !x.IsDeleted, ct)
                ?? throw new NotFoundException("Meeting business record not found.");
            if (meeting.Id.ToString(CultureInfo.InvariantCulture) != dto.BusinessRecordId)
                throw new BusinessRuleException("Meeting business record ID must be canonical.");
            type = meeting.MeetingType;
            subtype = meeting.Category;
            dto.Task = meeting.Title!;
            dto.Description = meeting.Description;
            dto.WorkflowInstanceId = meeting.WorkflowInstanceId;
        }
        int? allottedTatMinutes = null;
        long? tatRuleId = null;
        if (mode == TatMode.TypeOnly)
        {
            // No fake/default subtype: the rule identity is exactly module + type.
            type = type?.Trim();
            subtype = null;
            if (string.IsNullOrWhiteSpace(type))
                throw new BusinessRuleException("Type is required to resolve a TAT rule.");
            // Every Delegation EaTask is only ever created once, for the initial ("Actual") doer work
            // window — Review/Rework phases reuse this same EaTask and resolve their own TAT rules
            // separately (see DelegationService's phase tracking), never creating a second EaTask.
            var applicable = await rules.GetApplicableByTypeOnlyAsync(dto.ModuleId, type, Common.EaFms.DelegationTaskType.Actual, ct);
            if (applicable.Count == 0) throw new BusinessRuleException("No active TAT rule is configured for this module/type/taskType combination.");
            if (applicable.Count != 1) throw new BusinessRuleException("Multiple active TAT rules are configured for this module/type/taskType combination.");
            if (applicable[0].TatMinutes <= 0) throw new BusinessRuleException("The module TAT must be greater than zero.");
            allottedTatMinutes = applicable[0].TatMinutes;
            tatRuleId = applicable[0].Id;
        }
        if (requireTat)
        {
            if (!isApproval && (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(subtype)))
                throw new BusinessRuleException("Type and subtype are required to resolve an exact TAT rule; no legacy fallback is allowed.");
            var applicable = isApproval
                ? await rules.GetApplicableForApprovalAsync(dto.ModuleId, type, subtype, ct)
                : await rules.GetApplicableAsync(dto.ModuleId, type!.Trim(), subtype!.Trim(), ct);
            if (applicable.Count == 0) throw new BusinessRuleException("No active TAT rule is configured for this module/type/subtype combination.");
            if (applicable.Count != 1) throw new BusinessRuleException("Multiple active TAT rules are configured for this module/type/subtype combination.");
            if (applicable[0].TatMinutes <= 0) throw new BusinessRuleException("The module TAT must be greater than zero.");
            allottedTatMinutes = applicable[0].TatMinutes;
            tatRuleId = applicable[0].Id;
        }

        if (dto.WorkflowInstanceId.HasValue)
        {
            var workflows = await db.WorkflowInstances.FromSqlInterpolated(
                $"SELECT * FROM public.ea_workflow_instances WHERE \"Id\" = {dto.WorkflowInstanceId.Value} FOR SHARE").ToListAsync(ct);
            var workflow = workflows.SingleOrDefault();
            if (workflow is null || workflow.IsDeleted)
                throw new NotFoundException("The supplied workflow does not exist or is deleted.");
            if (workflow.BusinessModuleId != dto.ModuleId
                || !string.Equals(workflow.BusinessRecordId, dto.BusinessRecordId, StringComparison.Ordinal))
                throw new BusinessRuleException("The supplied workflow does not match the module and business record.");
        }

        var task = new EaTask
        {
            BusinessModuleId = dto.ModuleId, ModuleName = module.Name, BusinessRecordId = dto.BusinessRecordId,
            Task = dto.Task, Description = dto.Description,
            Type = type, Subtype = subtype, TatRuleId = tatRuleId,
            AllottedTatMinutes = allottedTatMinutes, WorkflowInstanceId = dto.WorkflowInstanceId,
            // Every new EaTask starts here, regardless of module. A module whose work is
            // already actionable the instant the record exists (Approval — see
            // ApprovalService.CreateAsync) transitions it to InProgress immediately
            // afterward, in the same transaction; this service never guesses that for them.
            ExecutionStatus = EaTaskExecutionStatus.NotStarted,
            IsActive = true, CreatedBy = user.ActorDisplay(), CreatedDate = Clock.UtcNowTz
        };
        await repository.AddAsync(task, ct);
        await db.SaveChangesAsync(ct);
        audit.AddAudit("EA_TASK_CREATE", "Task", nameof(EaTask), task.Id.ToString(CultureInfo.InvariantCulture), null,
            new { task.BusinessModuleId, task.BusinessRecordId, task.Task, task.AllottedTatMinutes, task.WorkflowInstanceId });
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);
        task.BusinessModule = module;
        return ToDto(task, new Dictionary<long, List<WorkPause>>(), Clock.UtcNowTz);
    }

    /// <summary>
    /// Derived only, never persisted: AllottedTatMinutes - TatUsedMinutes. Null unless
    /// both inputs are present. Positive = completed within TAT; negative = exceeded TAT.
    /// </summary>
    public static int? CalculateTatDifferenceMinutes(int? allottedTatMinutes, int? tatUsedMinutes) =>
        allottedTatMinutes.HasValue && tatUsedMinutes.HasValue
            ? allottedTatMinutes.Value - tatUsedMinutes.Value
            : null;

    private static EaTaskResponseDto ToDto(EaTask task, IReadOnlyDictionary<long, List<WorkPause>> pausesByWorkflow, DateTime now)
    {
        List<WorkPause> pauses = task.WorkflowInstanceId.HasValue
            && pausesByWorkflow.TryGetValue(task.WorkflowInstanceId.Value, out var wfPauses)
            ? wfPauses : new List<WorkPause>();

        bool? isPaused = null;
        int? pauseCount = null;
        int? totalPausedMinutes = null;
        if (task.WorkflowInstanceId.HasValue)
        {
            isPaused = pauses.Any(p => p.EndAt == null);
            pauseCount = pauses.Count;
            totalPausedMinutes = task.StartedAt.HasValue
                ? TotalPausedMinutesAllTypes(task.StartedAt.Value, task.CompletedAt ?? now, pauses)
                : 0;
        }

        var currentTatUsedMinutes = CalculateCurrentTatUsedMinutes(task, pauses, now);

        return new EaTaskResponseDto
        {
            EaTaskId = task.Id, ModuleId = task.BusinessModuleId, ModuleName = task.ModuleName,
            BusinessRecordId = task.BusinessRecordId, Task = task.Task, Description = task.Description,
            Type = task.Type, Subtype = task.Subtype, TatRuleId = task.TatRuleId,
            AllottedTatMinutes = task.AllottedTatMinutes, TatUsedMinutes = task.TatUsedMinutes,
            TatDifferenceMinutes = CalculateTatDifferenceMinutes(task.AllottedTatMinutes, task.TatUsedMinutes),
            ExecutionStatus = task.ExecutionStatus, StartedAt = task.StartedAt, CompletedAt = task.CompletedAt,
            IsPaused = isPaused, PauseCount = pauseCount, TotalPausedMinutes = totalPausedMinutes,
            CurrentTatUsedMinutes = currentTatUsedMinutes,
            CurrentTatDifferenceMinutes = CalculateTatDifferenceMinutes(task.AllottedTatMinutes, currentTatUsedMinutes),
            IsActive = task.IsActive,
            CreatedBy = task.CreatedBy, CreatedDate = task.CreatedDate,
            ModifiedBy = task.ModifiedBy, ModifiedDate = task.ModifiedDate
        };
    }

    /// <summary>
    /// Live TAT-consumed snapshot using the same canonical elapsed-minus-paused calculation
    /// as the frozen TatUsedMinutes (WorkPauseClassifier.GetPausedDuration — simple pauses
    /// only, matching the existing Meeting TAT semantics). Null before execution starts and
    /// for no-TAT modules; equals the frozen value once Completed.
    /// </summary>
    public static int? CalculateCurrentTatUsedMinutes(EaTask task, IReadOnlyCollection<WorkPause> pauses, DateTime now)
    {
        if (!task.AllottedTatMinutes.HasValue) return null;
        if (string.Equals(task.ExecutionStatus, EaTaskExecutionStatus.Completed, StringComparison.Ordinal))
            return task.TatUsedMinutes;
        if (!task.StartedAt.HasValue) return null;

        var end = task.CompletedAt ?? now;
        return CalculateActiveTatMinutes(task.StartedAt.Value, end, pauses);
    }

    /// <summary>
    /// The one canonical active-TAT calculation shared by the live value, the value frozen at completion
    /// (Meeting and Delegation) and EM Report: elapsed minus simple-pause time, never negative.
    /// </summary>
    public static int CalculateActiveTatMinutes(DateTime start, DateTime end, IReadOnlyCollection<WorkPause> pauses)
    {
        var paused = WorkPauseClassifier.GetPausedDuration(start, end, pauses);
        var used = end - start - paused;
        if (used < TimeSpan.Zero) used = TimeSpan.Zero;
        return (int)used.TotalMinutes;
    }

    /// <summary>
    /// General "how long has this task been paused/waiting" total — every WorkPause kind,
    /// unlike WorkPauseClassifier.GetPausedDuration's TAT-specific simple-pause-only rule.
    /// Mirrors the existing formula in WorkflowExecutionService.GetSummaryAsync.
    /// </summary>
    private static int TotalPausedMinutesAllTypes(DateTime start, DateTime end, IEnumerable<WorkPause> pauses) =>
        (int)pauses.Sum(p =>
        {
            var a = p.StartAt > start ? p.StartAt : start;
            var b = (p.EndAt ?? end) < end ? (p.EndAt ?? end) : end;
            return b > a ? (b - a).TotalMinutes : 0;
        });

    public async Task<List<EaTaskHistoryEventDto>> GetHistoryAsync(long id, CancellationToken ct) =>
        await new EaTaskHistoryBuilder(db).BuildAsync(id, ct);
}
