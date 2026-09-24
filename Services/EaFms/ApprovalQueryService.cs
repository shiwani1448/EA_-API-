using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public sealed class ApprovalQueryService(EaFmsDbContext db, IApprovalDocumentService documents, ITaskReviewService taskReview, ITatRuleRepository tatRules)
{
    // Canonical catalog name already used by ApprovalService.CreateAsync.
    private const string ApprovalModuleName = "EA Approval";

    public async Task<ApprovalListResponseDto> ListAsync(string? search, string? status, string? priority, string? approver, string? requestedBy, string? department, DateTime? createdFrom, DateTime? createdTo, DateTime? requiredFrom, DateTime? requiredTo, string? dueState, int page, int pageSize, CancellationToken ct)
    {
        var approvalModuleId = await ResolveApprovalModuleIdAsync(ct);
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = Requests(approvalModuleId);
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => EF.Functions.ILike(x.ReferenceNo, $"%{term}%") || (x.RequestTitle != null && EF.Functions.ILike(x.RequestTitle, $"%{term}%"))); }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.WorkflowStatus == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.Priority == priority);
        if (!string.IsNullOrWhiteSpace(approver)) query = query.Where(x => x.ApproverId == approver || x.ApproverName == approver);
        if (!string.IsNullOrWhiteSpace(requestedBy)) query = query.Where(x => x.RequestedBy == requestedBy || x.CreatedBy == requestedBy);
        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(x => x.Department == department);
        if (createdFrom.HasValue) query = query.Where(x => x.CreatedAt >= createdFrom);
        if (createdTo.HasValue) query = query.Where(x => x.CreatedAt <= createdTo);
        if (requiredFrom.HasValue) query = query.Where(x => x.RequiredApprovalDate >= requiredFrom);
        if (requiredTo.HasValue) query = query.Where(x => x.RequiredApprovalDate <= requiredTo);
        var now = Clock.UtcNowTz;
        if (string.Equals(dueState, "Overdue", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.EaTask.AllottedTatMinutes.HasValue && x.EaTask.StartedAt.HasValue && x.EaTask.StartedAt.Value.AddMinutes(x.EaTask.AllottedTatMinutes.Value) < now);
        else if (string.Equals(dueState, "Due", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.EaTask.AllottedTatMinutes.HasValue && x.EaTask.StartedAt.HasValue && x.EaTask.StartedAt.Value.AddMinutes(x.EaTask.AllottedTatMinutes.Value) >= now);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { Request = x, DocumentCount = db.Attachments.Count(a => a.RelatedModule == "Approval" && a.RelatedEntity == "ApprovalRequest" && a.RelatedEntityId == x.Id.ToString() && a.IsActive && !a.IsDeleted) }).ToListAsync(ct);
        var reviewSummaries = await taskReview.BatchGetCurrentAsync(rows.Select(r => r.Request.EaTaskId).Distinct().ToList(), ct) ?? new();
        var reviewAttachmentIds = ApprovalAttachments.ReviewAttachmentIds(await ApprovalAttachments.LoadRowsAsync(db, rows.Select(r => r.Request.Id).ToList(), ct));
        var tatViews = await BuildTatViewsAsync(rows.Select(r => r.Request).ToList(), ct);
        return new()
        {
            TotalCount = total, Page = page, PageSize = pageSize, Items = rows.Select(x =>
            {
                var review = reviewSummaries.GetValueOrDefault(x.Request.EaTaskId);
                ApprovalAttachments.ApplyReviewAttachment(review, x.Request.Id, reviewAttachmentIds);
                return ToList(x.Request, x.DocumentCount, now, review, tatViews.GetValueOrDefault(x.Request.Id));
            }).ToList()
        };
    }

    public async Task<ApprovalDetailDto?> DetailAsync(long id, CancellationToken ct)
    {
        var approvalModuleId = await ResolveApprovalModuleIdAsync(ct);
        var request = await Requests(approvalModuleId).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return null;
        var cycles = (await db.ApprovalCycles.AsNoTracking().Where(x => x.ApprovalRequestId == id).OrderBy(x => x.CycleNo).ThenBy(x => x.Id).ToListAsync(ct)).Select(Cycle).ToList();
        var now = Clock.UtcNowTz;
        var reviewSummary = await taskReview.GetCurrentAsync(request.EaTaskId, ct);
        var reviewAttachmentIds = ApprovalAttachments.ReviewAttachmentIds(await ApprovalAttachments.LoadRowsAsync(db, new[] { id }, ct));
        ApprovalAttachments.ApplyReviewAttachment(reviewSummary, id, reviewAttachmentIds);
        var tatView = (await BuildTatViewsAsync(new[] { request }, ct)).GetValueOrDefault(id);
        return ToDetail(request, cycles, await documents.ListAsync(id, ct), await HistoryAsync(request, ct), now, reviewSummary, tatView);
    }

    public async Task<IReadOnlyList<ApprovalCycleDto>?> CyclesAsync(long id, CancellationToken ct)
    {
        var moduleId = await ResolveApprovalModuleIdAsync(ct);
        if (!await Requests(moduleId).AnyAsync(x => x.Id == id, ct)) return null;
        return (await db.ApprovalCycles.AsNoTracking().Where(x => x.ApprovalRequestId == id).OrderBy(x => x.CycleNo).ThenBy(x => x.Id).ToListAsync(ct)).Select(Cycle).ToList();
    }

    public async Task<IReadOnlyList<ApprovalHistoryItemDto>?> HistoryForAsync(long id, CancellationToken ct)
    {
        var moduleId = await ResolveApprovalModuleIdAsync(ct);
        var request = await Requests(moduleId).SingleOrDefaultAsync(x => x.Id == id, ct);
        return request is null ? null : await HistoryAsync(request, ct);
    }

    public async Task<ApprovalDashboardDto> DashboardAsync(CancellationToken ct)
    {
        var query = Requests(await ResolveApprovalModuleIdAsync(ct)); var now = Clock.UtcNowTz;
        return new() { TotalRequests = await query.CountAsync(ct), PendingApproval = await query.CountAsync(x => x.WorkflowStatus == "PendingApproval", ct), Approved = await query.CountAsync(x => x.WorkflowStatus == "Approved", ct), Rejected = await query.CountAsync(x => x.WorkflowStatus == "Rejected", ct), Overdue = await query.CountAsync(x => x.EaTask.AllottedTatMinutes.HasValue && x.EaTask.StartedAt.HasValue && x.EaTask.StartedAt.Value.AddMinutes(x.EaTask.AllottedTatMinutes.Value) < now, ct) };
    }

    private async Task<long> ResolveApprovalModuleIdAsync(CancellationToken ct)
    {
        var ids = await db.BusinessModules.AsNoTracking().Where(x => x.IsActive && !x.IsDeleted && x.Name == ApprovalModuleName).Select(x => x.Id).Take(2).ToListAsync(ct);
        return ids.Count switch { 1 => ids[0], 0 => throw new BusinessRuleException("EA Approval business module is not registered as active in ea_business_modules."), _ => throw new BusinessRuleException("Multiple active EA Approval business modules are configured.") };
    }

    private IQueryable<ApprovalRequest> Requests(long approvalModuleId) => db.ApprovalRequests.AsNoTracking().Include(x => x.EaTask).ThenInclude(x => x.WorkflowInstance).ThenInclude(x => x!.Status).Where(x => !x.IsDeleted && x.EaTask.BusinessModuleId == approvalModuleId && x.EaTask.IsActive && !x.EaTask.IsDeleted);

    private async Task<List<ApprovalHistoryItemDto>> HistoryAsync(ApprovalRequest request, CancellationToken ct)
    {
        var result = await db.AuditLogs.AsNoTracking().Where(x => (x.Module == "Approval" || x.Module == "EA.Approval") && x.EntityName == nameof(ApprovalRequest) && x.EntityId == request.Id.ToString()).Select(x => new ApprovalHistoryItemDto { Id = x.Id, EventType = x.ActionType, OccurredAt = x.OccurredAt == default ? x.CreatedDate : x.OccurredAt, ActorId = x.ActorId, ActorName = x.ActorName, Description = x.Description }).ToListAsync(ct);
        if (request.EaTask.WorkflowInstanceId is long workflowId) result.AddRange(await db.WorkflowHistory.AsNoTracking().Where(x => x.WorkflowInstanceId == workflowId).Select(x => new ApprovalHistoryItemDto { Id = x.Id, EventType = "WorkflowTransition", OccurredAt = x.ChangedAt, ActorId = x.StageOwnerId, ActorName = x.StageOwnerName, Description = x.Notes }).ToListAsync(ct));
        return result.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToList();
    }

    private static ApprovalCycleDto Cycle(ApprovalCycle x) => new() { Id = x.Id, CycleNo = x.CycleNo, SubmittedAt = x.SubmittedAt, SubmittedBy = x.SubmittedBy, RequiredApprovalDate = x.RequiredApprovalDate, ApproverId = x.ApproverId, Status = x.Status, ChangeReason = x.ChangeReason, DecisionComment = x.DecisionComment, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt };
    private static ApprovalListItemDto ToList(ApprovalRequest x, int documents, DateTime now, TaskReviewSummaryDto? reviewSummary = null, ApprovalTatView? tat = null) => new() { ApprovalRequestId = x.Id, EaTaskId = x.EaTaskId, ReferenceNo = x.ReferenceNo, RequestTitle = x.RequestTitle, Description = x.Description, RequestedBy = x.RequestedBy, Department = x.Department, Type = x.RequestType, Priority = x.Priority, Approver = x.ApproverName ?? x.ApproverId, ApprovedBy = x.ApprovedBy, RejectedBy = x.RejectedBy, RequiredApprovalDate = x.RequiredApprovalDate, WorkflowStatus = x.WorkflowStatus, DueState = Due(x.EaTask, now), CurrentCycleNo = x.CurrentCycleNo, DocumentCount = documents, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt, UpdatedAt = x.UpdatedAt, ReviewSummary = reviewSummary ?? new TaskReviewSummaryDto(), PhaseTat = tat?.PhaseTat ?? new List<ApprovalPhaseTatDto>(), AllottedTatMinutes = tat?.AllottedTatMinutes, TatUsedMinutes = tat?.TatUsedMinutes, TatPausedMinutes = tat?.TatPausedMinutes, TatSummary = tat?.TatSummary ?? NoTatSummary(), IsPaused = tat?.IsPaused ?? false };
    private static ApprovalDetailDto ToDetail(ApprovalRequest x, IReadOnlyList<ApprovalCycleDto> cycles, IReadOnlyList<ApprovalDocumentResponseDto> documents, IReadOnlyList<ApprovalHistoryItemDto> history, DateTime now, TaskReviewSummaryDto? reviewSummary = null, ApprovalTatView? tat = null) => new() { ApprovalRequestId = x.Id, EaTaskId = x.EaTaskId, ReferenceNo = x.ReferenceNo, RequestTitle = x.RequestTitle, Description = x.Description, Justification = x.Justification, RequestedBy = x.RequestedBy, CreatedBy = x.CreatedBy, UpdatedBy = x.UpdatedBy, Type = x.RequestType, Priority = x.Priority, Department = x.Department, Amount = x.Amount, Currency = x.Currency, Approver = x.ApproverName ?? x.ApproverId, ApprovedBy = x.ApprovedBy, RejectedBy = x.RejectedBy, WorkflowStatus = x.WorkflowStatus, RequiredApprovalDate = x.RequiredApprovalDate, CreatedAt = x.CreatedAt, UpdatedAt = x.UpdatedAt, SubmittedAt = x.SubmittedAt, ApprovedAt = x.ApprovedAt, RejectedAt = x.RejectedAt, ClosedAt = x.ClosedAt, DueState = Due(x.EaTask, now), CurrentCycleNo = x.CurrentCycleNo, CurrentCycle = cycles.SingleOrDefault(c => c.CycleNo == x.CurrentCycleNo), LatestCycle = cycles.LastOrDefault(), Cycles = cycles, Documents = documents, History = history, ReviewSummary = reviewSummary ?? new TaskReviewSummaryDto(), Task = new() { EaTaskId = x.EaTaskId, BusinessModuleId = x.EaTask.BusinessModuleId, BusinessRecordId = x.EaTask.BusinessRecordId, Status = x.EaTask.WorkflowInstance?.Status?.Name ?? x.WorkflowStatus, Priority = x.Priority, DueDate = x.EaTask.AllottedTatMinutes.HasValue && x.EaTask.StartedAt.HasValue ? x.EaTask.StartedAt.Value.AddMinutes(x.EaTask.AllottedTatMinutes.Value) : null, AllottedTatMinutes = x.EaTask.AllottedTatMinutes, DueState = Due(x.EaTask, now) }, PhaseTat = tat?.PhaseTat ?? new List<ApprovalPhaseTatDto>(), AllottedTatMinutes = tat?.AllottedTatMinutes, TatUsedMinutes = tat?.TatUsedMinutes, TatPausedMinutes = tat?.TatPausedMinutes, TatSummary = tat?.TatSummary ?? NoTatSummary(), IsPaused = tat?.IsPaused ?? false };
    private static string Due(EaTask task, DateTime now) => !task.AllottedTatMinutes.HasValue || !task.StartedAt.HasValue ? "Unavailable" : task.StartedAt.Value.AddMinutes(task.AllottedTatMinutes.Value) < now ? "Overdue" : "Due";

    // ============================================================
    // PER-PHASE TAT (Actual / Review N / Rework N) + the 6 flat "current phase" fields — see
    // ApprovalPhaseTat's own doc comment. Mirrors DelegationService's LoadTatViewsAsync/
    // BuildPhaseTatAsync/ToPhaseTatDto region, adapted: Approval's central EaTask never carries a TAT
    // budget of its own (CreateWithoutTatAsync never resolves one), so the flat fields mirror the
    // CURRENT PHASE's own numbers instead of a whole-task EaTask snapshot.
    // ============================================================

    private sealed record ApprovalTatView(List<ApprovalPhaseTatDto> PhaseTat, int? AllottedTatMinutes, int? TatUsedMinutes, int? TatPausedMinutes, MeetingTatSummaryDto TatSummary, bool IsPaused);

    /// <summary>Same "unavailable TAT" fallback shape Delegation's NoTatSummary() uses.</summary>
    private static MeetingTatSummaryDto NoTatSummary() => new()
    {
        Tat = null, TotalTat = TimeSpan.Zero, TatDifference = TimeSpan.Zero, PauseTime = TimeSpan.Zero, PauseCount = 0
    };

    /// <summary>Batched for any number of Approval Requests — one phases query, one pauses query (pause anchor ids come from the already-Included EaTask.WorkflowInstance), no per-row lookup except the rare empty-PhaseTat fallback.</summary>
    private async Task<Dictionary<long, ApprovalTatView>> BuildTatViewsAsync(IReadOnlyCollection<ApprovalRequest> requests, CancellationToken ct)
    {
        var result = new Dictionary<long, ApprovalTatView>();
        if (requests.Count == 0) return result;

        var approvalRequestIds = requests.Select(r => r.Id).ToList();
        var phasesByRequest = (await db.ApprovalPhaseTats.AsNoTracking()
                .Where(p => approvalRequestIds.Contains(p.ApprovalRequestId)).OrderBy(p => p.Id).ToListAsync(ct))
            .GroupBy(p => p.ApprovalRequestId).ToDictionary(g => g.Key, g => g.ToList());

        var workflowIds = requests.Where(r => r.EaTask.WorkflowInstanceId.HasValue).Select(r => r.EaTask.WorkflowInstanceId!.Value).Distinct().ToList();
        var pausesByWorkflow = workflowIds.Count == 0
            ? new Dictionary<long, List<WorkPause>>()
            : (await db.WorkPauses.AsNoTracking()
                .Where(p => p.WorkflowInstanceId != null && workflowIds.Contains(p.WorkflowInstanceId.Value) && !p.IsDeleted)
                .ToListAsync(ct)).GroupBy(p => p.WorkflowInstanceId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var now = Clock.UtcNowTz;
        foreach (var request in requests)
        {
            var pauses = request.EaTask.WorkflowInstanceId.HasValue && pausesByWorkflow.TryGetValue(request.EaTask.WorkflowInstanceId.Value, out var p)
                ? p : new List<WorkPause>();
            var isPaused = request.EaTask.ExecutionStatus == EaTaskExecutionStatus.InProgress && pauses.Any(w => w.EndAt == null);

            if (!phasesByRequest.TryGetValue(request.Id, out var phases) || phases.Count == 0)
            {
                // Dormant Draft-not-yet-submitted path only — the live create flow (ApprovalService.CreateAsync)
                // always opens the Actual phase immediately, so this is a defensive fallback, not a real case.
                var applicable = await tatRules.GetApplicableForApprovalPhaseAsync(request.EaTask.BusinessModuleId, request.RequestType, request.Department, DelegationTaskType.Actual, ct);
                var allotted = applicable.Count == 1 ? applicable[0].TatMinutes : (int?)null;
                result[request.Id] = new ApprovalTatView(new List<ApprovalPhaseTatDto>(), allotted, null, null, NoTatSummary(), false);
                continue;
            }

            var phaseDtos = phases.Select(ph => ToPhaseTatDto(ph, pauses, now)).ToList();
            var current = phaseDtos[^1];
            var currentEntity = phases[^1];
            var currentPauses = PausesOverlapping(pauses, currentEntity.StartedAt, currentEntity.EndedAt ?? now);
            var tatSummary = TatSummaryCalculator.Calculate(currentEntity.AllottedTatMinutes ?? 0, currentEntity.StartedAt, currentEntity.EndedAt, currentPauses, now);

            result[request.Id] = new ApprovalTatView(phaseDtos, current.AllottedTatMinutes, current.TatUsedMinutes, current.TatPausedMinutes, tatSummary, isPaused);
        }
        return result;
    }

    private static ApprovalPhaseTatDto ToPhaseTatDto(ApprovalPhaseTat phase, IReadOnlyCollection<WorkPause> pauses, DateTime now)
    {
        if (phase.EndedAt.HasValue)
        {
            return new ApprovalPhaseTatDto
            {
                TaskType = phase.TaskType, ReviewCycleNumber = phase.ReviewCycleNumber,
                StartedAt = phase.StartedAt, EndedAt = phase.EndedAt,
                AllottedTatMinutes = phase.AllottedTatMinutes, TatUsedMinutes = phase.TatUsedMinutes,
                TatPausedMinutes = phase.TatPausedMinutes, PauseCount = phase.PauseCount,
                TatUsedSeconds = phase.TatUsedSeconds, TatPausedSeconds = phase.TatPausedSeconds,
                TatDifferenceSeconds = phase.AllottedTatMinutes.HasValue && phase.TatUsedSeconds.HasValue
                    ? phase.AllottedTatMinutes.Value * 60m - phase.TatUsedSeconds.Value : null,
                TatDifferenceMinutes = phase.AllottedTatMinutes.HasValue && phase.TatUsedMinutes.HasValue
                    ? phase.AllottedTatMinutes - phase.TatUsedMinutes : null,
            };
        }
        if (!phase.StartedAt.HasValue)
        {
            // Open but not yet started (Review/Rework waiting for their explicit Start action) — zero
            // elapsed, full budget still available; never call TatSummaryCalculator with no window.
            return new ApprovalPhaseTatDto
            {
                TaskType = phase.TaskType, ReviewCycleNumber = phase.ReviewCycleNumber,
                StartedAt = null, EndedAt = null,
                AllottedTatMinutes = phase.AllottedTatMinutes,
                TatUsedMinutes = phase.AllottedTatMinutes.HasValue ? 0 : null,
                TatPausedMinutes = 0, PauseCount = 0,
                TatUsedSeconds = phase.AllottedTatMinutes.HasValue ? 0m : null, TatPausedSeconds = 0m,
                TatDifferenceSeconds = phase.AllottedTatMinutes.HasValue ? phase.AllottedTatMinutes.Value * 60m : null,
                TatDifferenceMinutes = phase.AllottedTatMinutes,
            };
        }
        // The current, still-open phase — live-ticking, same formula, window end is "now" instead of a frozen EndedAt.
        var relevant = PausesOverlapping(pauses, phase.StartedAt, now);
        var summary = TatSummaryCalculator.Calculate(phase.AllottedTatMinutes ?? 0, phase.StartedAt, null, relevant, now);
        int? used = phase.AllottedTatMinutes.HasValue ? (int)summary.Tat!.Value.TotalMinutes : null;
        decimal? usedSeconds = phase.AllottedTatMinutes.HasValue ? DurationSeconds(summary.Tat!.Value) : null;
        return new ApprovalPhaseTatDto
        {
            TaskType = phase.TaskType, ReviewCycleNumber = phase.ReviewCycleNumber,
            StartedAt = phase.StartedAt, EndedAt = null,
            AllottedTatMinutes = phase.AllottedTatMinutes, TatUsedMinutes = used,
            TatPausedMinutes = (int)summary.PauseTime.TotalMinutes, PauseCount = summary.PauseCount,
            TatUsedSeconds = usedSeconds, TatPausedSeconds = DurationSeconds(summary.PauseTime),
            TatDifferenceSeconds = phase.AllottedTatMinutes.HasValue && usedSeconds.HasValue
                ? phase.AllottedTatMinutes.Value * 60m - usedSeconds.Value : null,
            TatDifferenceMinutes = phase.AllottedTatMinutes.HasValue && used.HasValue ? phase.AllottedTatMinutes - used : null,
        };
    }

    /// <summary>Simple pauses overlapping [start, windowEnd) — an open pause (EndAt null) always overlaps since it has no upper bound yet.</summary>
    private static List<WorkPause> PausesOverlapping(IReadOnlyCollection<WorkPause> pauses, DateTime? start, DateTime windowEnd) =>
        pauses.Where(p => start.HasValue && WorkPauseClassifier.IsSimplePause(p) && p.StartAt < windowEnd && (p.EndAt ?? DateTime.MaxValue) > start).ToList();

    // Decimal tick conversion avoids floating-point loss and retains TimeSpan's 100 ns precision.
    private static decimal DurationSeconds(TimeSpan duration) => duration.Ticks / (decimal)TimeSpan.TicksPerSecond;
}
