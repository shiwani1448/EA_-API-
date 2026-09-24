using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Jarvis5.Services.EaFms;

public class ApprovalLifecycleService : IApprovalLifecycleService
{
    // Same PDF limits as Delegation's completion/review attachments (25 MiB).
    private const long MaxAttachmentBytes = 25 * 1024 * 1024;

    private readonly EaFmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly ITaskReviewService _taskReview;
    private readonly ICurrentUserService _user;
    private readonly IWebHostEnvironment _env;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITatRuleRepository _tatRules;

    // ApprovalQueryService is resolved lazily via IServiceProvider (not constructor-injected) to break
    // a real DI cycle: ApprovalDocumentService -> IApprovalLifecycleService -> ApprovalQueryService ->
    // IApprovalDocumentService. A direct constructor dependency here would make that cycle
    // unconstructible; resolving it on demand (after construction has already completed, and — since
    // IApprovalLifecycleService is scoped and already cached in the current scope by the time
    // ApprovalQueryService transitively asks for it again — never re-entrant) avoids that entirely.
    public ApprovalLifecycleService(EaFmsDbContext db, IAuditService audit, ITaskReviewService taskReview,
        ICurrentUserService user, IWebHostEnvironment env, IServiceProvider serviceProvider, ITatRuleRepository tatRules)
    {
        _db = db;
        _audit = audit;
        _taskReview = taskReview;
        _user = user;
        _env = env;
        _serviceProvider = serviceProvider;
        _tatRules = tatRules;
    }

    private ApprovalQueryService Query => _serviceProvider.GetRequiredService<ApprovalQueryService>();

    public Task<bool> IsDocumentOperationAllowed(long approvalRequestId, string operation, CancellationToken ct = default)
    {
        // Conservative default: allow uploads/deletes in Draft, PendingApproval, ChangesRequested, Resubmitted
        // disallow in Approved or Rejected. This centralizes the rule so other services can reuse it.
        var allowed = new[] { "Draft", "PendingApproval", "ChangesRequested", "Resubmitted" };
        var req = _db.ApprovalRequests.AsNoTracking().FirstOrDefault(a => a.Id == approvalRequestId && !a.IsDeleted);
        if (req == null) return Task.FromResult(false);
        if (string.IsNullOrWhiteSpace(req.WorkflowStatus)) return Task.FromResult(false);
        if (req.WorkflowStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase) || req.WorkflowStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);
        return Task.FromResult(allowed.Contains(req.WorkflowStatus));
    }

    public async Task<ApprovalRequest> SubmitAsync(long approvalRequestId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var req = await RequireApprovalAsync(approvalRequestId, ct);
        if (!string.Equals(req.WorkflowStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Only Draft approvals may be submitted.");

        // Defensive symmetry with the live create path (ApprovalService.CreateAsync), which already
        // opens the Actual phase at creation — this dormant Draft->PendingApproval path stays correct
        // in case a Draft-creating caller is ever wired up. Same duplicate guard Delegation's StartAsync uses.
        if (await _db.ApprovalPhaseTats.AnyAsync(p => p.ApprovalRequestId == req.Id, ct))
            throw new BusinessRuleException("Cannot submit an Approval with existing phase history.");

        // Cycle 1
        var now = Clock.UtcNowTz;
        var cycle = new ApprovalCycle
        {
            ApprovalRequestId = req.Id,
            CycleNo = 1,
            SubmittedAt = now,
            SubmittedBy = req.CreatedBy ?? "system",
            Status = "PendingApproval",
            CreatedAt = now
        };
        await _db.ApprovalCycles.AddAsync(cycle, ct);

        req.WorkflowStatus = "PendingApproval";
        _db.ApprovalRequests.Update(req);

        // Defensive symmetry: the live create path (ApprovalService.CreateAsync) already
        // opens the Actual phase idle (NotStarted) and never reaches Draft, so this is currently
        // unreachable through it — kept correct in case a Draft-creating caller is ever wired up.
        // eaTask.ExecutionStatus stays NotStarted here too: the Actual phase opens idle below and
        // only an explicit StartActualAsync call flips it to InProgress, exactly like the live path.
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);

        await OpenPhaseAsync(req.Id, eaTask.BusinessModuleId, req.RequestType, req.Department, DelegationTaskType.Actual, 0, now, ct);

        _audit.AddAudit("APPROVAL_SUBMIT", "Approval", nameof(ApprovalRequest), req.Id.ToString(), null, new { req.ReferenceNo, req.Id }, "Approval submitted");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return req;
    }

    public async Task<ApprovalRequest> ApproveAsync(long approvalRequestId, ApprovalDecisionDto dto, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var req = await RequireApprovalAsync(approvalRequestId, ct);
        if (!string.Equals(req.WorkflowStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Only PendingApproval approvals may be approved.");

        var cycle = await _db.ApprovalCycles.Where(c => c.ApprovalRequestId == req.Id).OrderByDescending(c => c.CycleNo).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Approval has no cycle to approve.");

        var decidedAt = Clock.UtcNowTz;
        cycle.Status = "Approved";
        cycle.DecisionComment = dto.Comment?.Trim();
        cycle.UpdatedAt = decidedAt;
        _db.ApprovalCycles.Update(cycle);

        req.WorkflowStatus = "Approved";
        // Server-owned decision timestamp, written in the same transaction as the status change.
        req.ApprovedAt = decidedAt;
        req.RejectedAt = null;
        // Decision actor = frontend-supplied operator (distinct from the designated ApproverName). Cannot be both.
        req.RejectedBy = null;
        if (EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName).DisplayName is { } approver) req.ApprovedBy = approver;
        _db.ApprovalRequests.Update(req);

        // Approved/Rejected are business decisions, not execution states (a decision task's
        // work is finished either way) — both map to central Completed.
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);
        eaTask.ExecutionStatus = EaTaskExecutionStatus.Completed;
        eaTask.CompletedAt ??= decidedAt;

        // Unlike Delegation (whose only path to Completed is via the Task Review's ApproveReviewAsync,
        // which already closes the open phase), Approval's PRIMARY completion path is this ordinary
        // business decision — most Approvals never touch submit-for-review/review/approve at all.
        // Freeze whatever phase (Actual/Review/Rework) is currently open so its TAT stops ticking, and
        // close the pause anchor if one exists — otherwise TAT would tick upward forever post-decision.
        await FinalizePhaseAndAnchorAsync(req.Id, eaTask, decidedAt, ct);

        _audit.AddAudit("APPROVAL_APPROVE", "Approval", nameof(ApprovalRequest), req.Id.ToString(), null, new { req.ReferenceNo, req.Id, cycle.CycleNo, req.ApprovedBy }, "Approval approved");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return req;
    }

    public async Task<ApprovalRequest> RejectAsync(long approvalRequestId, ApprovalDecisionDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Comment)) throw new BusinessRuleException("Reject requires a non-empty reason.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var req = await RequireApprovalAsync(approvalRequestId, ct);
        if (!string.Equals(req.WorkflowStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Only PendingApproval approvals may be rejected.");

        var cycle = await _db.ApprovalCycles.Where(c => c.ApprovalRequestId == req.Id).OrderByDescending(c => c.CycleNo).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Approval has no cycle to reject.");

        var decidedAt = Clock.UtcNowTz;
        cycle.Status = "Rejected";
        cycle.DecisionComment = dto.Comment!.Trim();
        cycle.UpdatedAt = decidedAt;
        _db.ApprovalCycles.Update(cycle);

        req.WorkflowStatus = "Rejected";
        req.RejectedAt = decidedAt;
        req.ApprovedAt = null;
        req.ApprovedBy = null;
        if (EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName).DisplayName is { } rejecter) req.RejectedBy = rejecter;
        _db.ApprovalRequests.Update(req);

        // Approved/Rejected are business decisions, not execution states (a decision task's
        // work is finished either way) — both map to central Completed.
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);
        eaTask.ExecutionStatus = EaTaskExecutionStatus.Completed;
        eaTask.CompletedAt ??= decidedAt;

        // See ApproveAsync's comment above — the same freeze applies to a Rejected decision.
        await FinalizePhaseAndAnchorAsync(req.Id, eaTask, decidedAt, ct);

        _audit.AddAudit("APPROVAL_REJECT", "Approval", nameof(ApprovalRequest), req.Id.ToString(), null, new { req.ReferenceNo, req.Id, cycle.CycleNo, req.RejectedBy }, "Approval rejected");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return req;
    }

    public async Task<ApprovalRequest> RequestChangesAsync(long approvalRequestId, ApprovalDecisionDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Comment)) throw new BusinessRuleException("Request changes requires a non-empty reason.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var req = await RequireApprovalAsync(approvalRequestId, ct);
        if (!string.Equals(req.WorkflowStatus, "PendingApproval", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Only PendingApproval approvals may request changes.");

        var cycle = await _db.ApprovalCycles.Where(c => c.ApprovalRequestId == req.Id).OrderByDescending(c => c.CycleNo).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Approval has no cycle to mark changes requested.");

        cycle.Status = "ChangesRequested";
        cycle.ChangeReason = dto.Comment!.Trim();
        cycle.UpdatedAt = Clock.UtcNowTz;
        _db.ApprovalCycles.Update(cycle);

        req.WorkflowStatus = "ChangesRequested";
        _db.ApprovalRequests.Update(req);

        _audit.AddAudit("APPROVAL_REQUEST_CHANGES", "Approval", nameof(ApprovalRequest), req.Id.ToString(), null, new { req.ReferenceNo, req.Id, cycle.CycleNo }, "Approval changes requested");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return req;
    }

    public async Task<ApprovalRequest> ResubmitAsync(long approvalRequestId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var req = await RequireApprovalAsync(approvalRequestId, ct);
        if (!string.Equals(req.WorkflowStatus, "ChangesRequested", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Only ChangesRequested approvals may be resubmitted.");

        // Re-read max cycle inside transaction
        var maxCycle = await _db.ApprovalCycles.Where(c => c.ApprovalRequestId == req.Id).MaxAsync(c => (int?)c.CycleNo, ct) ?? 0;
        var newCycleNo = maxCycle + 1;

        // Concurrency: unique index on (ApprovalRequestId, CycleNo) will prevent duplicates; detect and translate.
        var now = Clock.UtcNowTz;
        var cycle = new ApprovalCycle
        {
            ApprovalRequestId = req.Id,
            CycleNo = newCycleNo,
            SubmittedAt = now,
            SubmittedBy = req.CreatedBy ?? "system",
            Status = "PendingApproval",
            CreatedAt = now
        };
        await _db.ApprovalCycles.AddAsync(cycle, ct);

        req.WorkflowStatus = "PendingApproval";
        _db.ApprovalRequests.Update(req);

        try
        {
            _audit.AddAudit("APPROVAL_RESUBMIT", "Approval", nameof(ApprovalRequest), req.Id.ToString(), null, new { req.ReferenceNo, req.Id, cycle.CycleNo }, "Approval resubmitted");
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new BusinessRuleException("Concurrent resubmission detected; retry the operation.");
        }

        return req;
    }

    public async Task<List<ApprovalCycle>> GetCyclesAsync(long approvalRequestId, CancellationToken ct = default)
    {
        return await _db.ApprovalCycles.Where(c => c.ApprovalRequestId == approvalRequestId).OrderBy(c => c.CycleNo).ToListAsync(ct);
    }

    public async Task<List<Jarvis5.Dtos.EaFms.WorkflowHistoryResponseDto>> GetHistoryAsync(long approvalRequestId, CancellationToken ct = default)
    {
        // Use shared workflow history & audit logs. For now, return audit logs filtered to this approval entity.
        var logs = await _db.AuditLogs.AsNoTracking().Where(a => a.Module == "Approval" && a.EntityName == nameof(ApprovalRequest) && a.EntityId == approvalRequestId.ToString()).OrderBy(a => a.CreatedDate).ToListAsync(ct);
        return logs.Select(l => new Jarvis5.Dtos.EaFms.WorkflowHistoryResponseDto { Id = l.Id, ChangedAt = l.CreatedDate, Notes = l.Description, StageOwnerId = l.ActorId, StageOwnerName = l.ActorName }).ToList();
    }

    // ============================================================
    // TASK REVIEW / REWORK (Phase 1) — entirely separate from the business
    // WorkflowStatus/ApprovalCycle lifecycle above (Submit/Approve/Reject/
    // RequestChanges/Resubmit). Never reuses PendingApproval/Approved/Rejected/
    // ChangesRequested for review state, and never touches WorkflowStatus/ApprovalCycle.
    // ============================================================

    private async Task<long> RequireApprovalEaTaskIdAsync(long approvalRequestId, CancellationToken ct)
    {
        var eaTaskId = await _db.ApprovalRequests.AsNoTracking()
            .Where(a => a.Id == approvalRequestId && !a.IsDeleted)
            .Select(a => (long?)a.EaTaskId)
            .SingleOrDefaultAsync(ct);
        return eaTaskId ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
    }

    private async Task<ApprovalRequest> RequireApprovalAsync(long approvalRequestId, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            var rows = await _db.ApprovalRequests.FromSqlInterpolated(
                $"SELECT * FROM public.ea_approval_requests WHERE \"Id\" = {approvalRequestId} AND NOT \"IsDeleted\" FOR UPDATE")
                .ToListAsync(ct);
            var req = rows.SingleOrDefault() ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
            await _db.Entry(req).ReloadAsync(ct);
            return req;
        }
        return await _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId && !a.IsDeleted, ct)
            ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
    }

    public async Task<TaskReviewSummaryDto> SubmitForReviewAsync(long approvalRequestId, SubmitForReviewRequestDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var req = await RequireApprovalAsync(approvalRequestId, ct);
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);
        await RequireNoOpenPauseAsync(eaTask.WorkflowInstanceId, ct);
        var currentReview = await _db.TaskReviews.AsNoTracking().Where(r => r.EaTaskId == req.EaTaskId)
            .OrderByDescending(r => r.ReviewCycleNo).FirstOrDefaultAsync(ct);
        if (currentReview is not null && currentReview.ReviewStatus != TaskReviewStatus.ReworkRequested)
            throw new BusinessRuleException("Work can only be submitted initially or after a rework request.");

        RequireInProgress(eaTask, "submitted for review");
        var phase = await RequireOpenPhaseAsync(approvalRequestId,
            currentReview is null ? DelegationTaskType.Actual : DelegationTaskType.Rework,
            currentReview?.ReviewCycleNo ?? 0, ct);
        if (!phase.StartedAt.HasValue)
            throw new BusinessRuleException("Start the current phase before submitting it for review.");
        var summary = await _taskReview.SubmitForReviewAsync(req.EaTaskId, dto, ct);
        var now = Clock.UtcNowTz;
        await ClosePhaseAsync(phase, eaTask.WorkflowInstanceId, now, ct);
        await OpenPhaseAsync(approvalRequestId, eaTask.BusinessModuleId, req.RequestType, req.Department, DelegationTaskType.Review, summary.ReviewCycleNumber, now, ct);

        _audit.AddAudit("APPROVAL_SUBMIT_FOR_REVIEW", "Approval", nameof(ApprovalRequest), req.Id.ToString(CultureInfo.InvariantCulture),
            null, new { summary.ReviewCycleNumber }, "Approval work submitted for review");

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return summary;
    }

    /// <summary>
    /// Approves the current pending review cycle and closes its Review phase. Deliberately does NOT
    /// touch WorkflowStatus/ApprovalCycle/eaTask.ExecutionStatus — Approval's Task Review is entirely
    /// separate from the business Submit/Approve/Reject/RequestChanges/Resubmit lifecycle; that decision
    /// stays owned exclusively by ApproveAsync/RejectAsync below (a real, intentional divergence from
    /// Delegation, where review-approve DOES finalize completion).
    /// </summary>
    public async Task<TaskReviewSummaryDto> ApproveReviewAsync(long approvalRequestId, ApproveTaskReviewRequestDto dto, IFormFile? attachment, CancellationToken ct = default)
    {
        var attachmentBytes = attachment is null ? null : await ReadValidatedPdfAsync(attachment, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var req = await RequireApprovalAsync(approvalRequestId, ct);
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);
        await RequireNoOpenPauseAsync(eaTask.WorkflowInstanceId, ct);
        var phase = await RequireReviewPhaseAsync(approvalRequestId, req.EaTaskId, ct);
        var summary = await _taskReview.ApproveAsync(req.EaTaskId, dto, ct);
        var now = Clock.UtcNowTz;
        await ClosePhaseAsync(phase, eaTask.WorkflowInstanceId, now, ct);

        Attachment? attachmentRow = null;
        string? objectKey = null;
        try
        {
            if (attachmentBytes is not null)
            {
                attachmentRow = await AddReviewAttachmentAsync(approvalRequestId, ApprovalAttachments.ReviewAttachmentPurpose, summary.ReviewCycleNumber, attachment!, attachmentBytes, now, ct);
                objectKey = attachmentRow.ObjectKey;
            }

            _audit.AddAudit("APPROVAL_REVIEW_APPROVE", "Approval", nameof(ApprovalRequest), req.Id.ToString(CultureInfo.InvariantCulture),
                null, new { summary.ReviewCycleNumber }, "Approval review approved");

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            if (objectKey is not null) TryDeleteFile(objectKey);
            throw;
        }

        summary.AttachmentId = attachmentRow?.Id;
        return summary;
    }

    /// <summary>
    /// Sends the current pending review cycle back for rework: closes the Review phase and opens a
    /// Rework phase at the same cycle number. Same no-WorkflowStatus-touch rule as ApproveReviewAsync.
    /// </summary>
    public async Task<TaskReviewSummaryDto> RequestTaskReworkAsync(long approvalRequestId, RequestTaskReworkRequestDto dto, IFormFile? attachment, CancellationToken ct = default)
    {
        var attachmentBytes = attachment is null ? null : await ReadValidatedPdfAsync(attachment, ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var req = await RequireApprovalAsync(approvalRequestId, ct);
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);
        await RequireNoOpenPauseAsync(eaTask.WorkflowInstanceId, ct);
        var phase = await RequireReviewPhaseAsync(approvalRequestId, req.EaTaskId, ct);
        var summary = await _taskReview.RequestReworkAsync(req.EaTaskId, dto, ct);
        var now = Clock.UtcNowTz;

        await ClosePhaseAsync(phase, eaTask.WorkflowInstanceId, now, ct);
        await OpenPhaseAsync(approvalRequestId, eaTask.BusinessModuleId, req.RequestType, req.Department, DelegationTaskType.Rework, summary.ReviewCycleNumber, now, ct);

        Attachment? attachmentRow = null;
        string? objectKey = null;
        try
        {
            if (attachmentBytes is not null)
            {
                attachmentRow = await AddReviewAttachmentAsync(approvalRequestId, ApprovalAttachments.ReworkAttachmentPurpose, summary.ReviewCycleNumber, attachment!, attachmentBytes, now, ct);
                objectKey = attachmentRow.ObjectKey;
            }

            _audit.AddAudit("APPROVAL_REVIEW_REWORK", "Approval", nameof(ApprovalRequest), req.Id.ToString(CultureInfo.InvariantCulture),
                null, new { summary.ReviewCycleNumber }, "Approval sent for rework");

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            if (objectKey is not null) TryDeleteFile(objectKey);
            throw;
        }

        summary.AttachmentId = attachmentRow?.Id;
        return summary;
    }

    public async Task<List<TaskReviewHistoryItemDto>> GetReviewHistoryAsync(long approvalRequestId, CancellationToken ct = default)
    {
        var eaTaskId = await RequireApprovalEaTaskIdAsync(approvalRequestId, ct);
        var history = await _taskReview.GetHistoryAsync(eaTaskId, ct);
        var reviewAttachmentIds = ApprovalAttachments.ReviewAttachmentIds(await ApprovalAttachments.LoadRowsAsync(_db, new[] { approvalRequestId }, ct));
        foreach (var cycle in history)
            cycle.AttachmentId = reviewAttachmentIds.TryGetValue((approvalRequestId, cycle.ReviewCycleNumber), out var id) ? id : null;
        return history;
    }

    /// <summary>Adds (tracked, not yet saved) the assignee's review/rework decision attachment.</summary>
    private async Task<Attachment> AddReviewAttachmentAsync(long approvalRequestId, string purpose, int reviewCycleNumber, IFormFile file, byte[] bytes, DateTime now, CancellationToken ct)
    {
        var objectKey = await WriteApprovalAttachmentAsync(approvalRequestId, bytes, ct);
        var actor = Actor();
        var attachment = new Attachment
        {
            RelatedModule = ApprovalAttachments.RelatedModule,
            RelatedEntity = ApprovalAttachments.RelatedEntity,
            RelatedEntityId = approvalRequestId.ToString(CultureInfo.InvariantCulture),
            OriginalFileName = Path.GetFileName(file.FileName.Replace('\\', '/')),
            ObjectKey = objectKey,
            ContentType = "application/pdf",
            Size = bytes.LongLength,
            AccessUrl = null, // no storage path is exposed; download via the existing generic attachment/document download endpoint
            UploadedBy = actor,
            UploadedAt = now,
            Metadata = $"{{\"purpose\":\"{purpose}\",\"approvalRequestId\":{approvalRequestId},\"reviewCycleNumber\":{reviewCycleNumber}}}",
            IsActive = true,
            IsDeleted = false,
            CreatedBy = actor,
            CreatedDate = now
        };
        _db.Attachments.Add(attachment);
        return attachment;
    }

    // ============================================================
    // LIFECYCLE — PAUSE / RESUME (shared WorkPause architecture, mirrors Delegation's)
    // ============================================================
    //
    // Approval has no single WorkflowStatus value that means "the whole thing is running" the way
    // Delegation's Status == InProgress does — PendingApproval/ChangesRequested/Resubmitted are all
    // "InProgress" at the execution level; Approved/Rejected map to central Completed. So the gate here
    // is the central EaTask.ExecutionStatus, not WorkflowStatus. The pause anchor (WorkflowInstance) is
    // lazily created on first Pause exactly like Delegation's, linked via EaTask.WorkflowInstanceId.

    public async Task<ApprovalDetailDto> PauseAsync(long approvalRequestId, ApprovalPauseRequestDto? request, CancellationToken ct = default)
    {
        var reason = string.IsNullOrWhiteSpace(request?.PauseReason) ? "Approval paused" : request!.PauseReason!.Trim();
        if (reason.Length > 2000) throw new BadRequestException("pauseReason must be at most 2000 characters.");

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var req = await RequireApprovalAsync(approvalRequestId, ct);
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);
        RequireInProgress(eaTask, "paused");
        var currentPhase = await _db.ApprovalPhaseTats.FirstOrDefaultAsync(p => p.ApprovalRequestId == approvalRequestId && p.EndedAt == null, ct);
        if (currentPhase is not null && !currentPhase.StartedAt.HasValue)
            throw new BusinessRuleException("Start the current phase before pausing it.");

        var now = Clock.UtcNowTz;
        var anchor = await EnsureAnchorAsync(req, eaTask, now, ct);

        if (await _db.WorkPauses.AnyAsync(p => p.WorkflowInstanceId == anchor.Id && !p.IsDeleted && p.EndAt == null, ct))
            throw new BusinessRuleException("Approval is already paused. Resume it first.");

        var actor = Actor();
        var pause = new WorkPause
        {
            WorkflowInstanceId = anchor.Id,
            IntakeRequestId = null,
            FollowupId = null,
            StartAt = now,
            Reason = reason,
            CreatedBy = actor,
            CreatedDate = now
        };
        _db.WorkPauses.Add(pause);
        AddSameStatusHistory(anchor, reason, "SIMPLE_PAUSE", actor, now);

        req.UpdatedBy = actor;
        req.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        _audit.AddAudit("APPROVAL_PAUSE", "Approval", nameof(ApprovalRequest), req.Id.ToString(CultureInfo.InvariantCulture),
            new { IsPaused = false }, new { IsPaused = true, PauseId = pause.Id, pause.StartAt, pause.Reason }, "Approval paused");
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await Query.DetailAsync(approvalRequestId, ct) ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
    }

    public async Task<ApprovalDetailDto> ResumeAsync(long approvalRequestId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var req = await RequireApprovalAsync(approvalRequestId, ct);
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);
        RequireInProgress(eaTask, "resumed");

        var open = eaTask.WorkflowInstanceId.HasValue
            ? await _db.WorkPauses
                .Where(p => p.WorkflowInstanceId == eaTask.WorkflowInstanceId && !p.IsDeleted && p.EndAt == null)
                .OrderByDescending(p => p.StartAt).ThenByDescending(p => p.Id)
                .ToListAsync(ct)
            : new List<WorkPause>();
        if (open.Count == 0)
            throw new BusinessRuleException("No open pause found. The Approval is not paused.");
        if (open.Count > 1)
            throw new BusinessRuleException("Multiple open operational stops exist; resolve manually.");

        var pause = open[0];
        var now = Clock.UtcNowTz;
        var actor = Actor();
        pause.EndAt = now;
        pause.ResumedById = _user.UserId.ToString(CultureInfo.InvariantCulture);
        pause.ResumedByName = _user.UserName;
        pause.ModifiedBy = actor;
        pause.ModifiedDate = now;

        var anchor = await _db.WorkflowInstances.FirstAsync(w => w.Id == eaTask.WorkflowInstanceId!.Value, ct);
        AddSameStatusHistory(anchor, "Work resumed", "SIMPLE_RESUME", actor, now);

        req.UpdatedBy = actor;
        req.UpdatedAt = now;

        _audit.AddAudit("APPROVAL_RESUME", "Approval", nameof(ApprovalRequest), req.Id.ToString(CultureInfo.InvariantCulture),
            new { IsPaused = true, PauseId = pause.Id }, new { IsPaused = false, PauseId = pause.Id, pause.EndAt, pause.ResumedById, pause.ResumedByName }, "Approval resumed");
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await Query.DetailAsync(approvalRequestId, ct) ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
    }

    private static void RequireInProgress(EaTask eaTask, string action)
    {
        if (eaTask.ExecutionStatus != EaTaskExecutionStatus.InProgress)
            throw new BusinessRuleException($"Approval cannot be {action} while its central task is '{eaTask.ExecutionStatus}'. It must be InProgress.");
    }

    /// <summary>Returns the Approval's pause anchor, creating and linking it on first use.</summary>
    private async Task<WorkflowInstance> EnsureAnchorAsync(ApprovalRequest req, EaTask eaTask, DateTime now, CancellationToken ct)
    {
        if (eaTask.WorkflowInstanceId.HasValue)
            return await _db.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == eaTask.WorkflowInstanceId.Value && !w.IsDeleted, ct)
                ?? throw new BusinessRuleException("The Approval's pause workflow is missing or deleted.");

        var inProgress = await SingleStatusAsync("In Progress", ct);
        var startedAt = eaTask.StartedAt ?? now;
        var anchor = new WorkflowInstance
        {
            BusinessModuleId = eaTask.BusinessModuleId,
            BusinessRecordId = req.Id.ToString(CultureInfo.InvariantCulture),
            StatusId = inProgress.Id,
            StartedAt = startedAt,
            TatStartedAt = startedAt,
            UpdatedAt = now,
            // Not load-bearing: Approval has no single "doer" concept, just carried for the shared anchor entity's sake.
            DoerId = req.ApproverId,
            DoerName = req.ApproverName,
            IsActive = true,
            CreatedBy = Actor(),
            CreatedDate = now
        };
        _db.WorkflowInstances.Add(anchor);
        await _db.SaveChangesAsync(ct);
        eaTask.WorkflowInstanceId = anchor.Id;
        return anchor;
    }

    private async Task CompleteAnchorAsync(long workflowId, DateTime now, CancellationToken ct)
    {
        var anchor = await _db.WorkflowInstances.FirstOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted, ct);
        if (anchor is null) return;
        var completed = await SingleStatusAsync("Completed", ct);
        var fromStatusId = anchor.StatusId;
        anchor.StatusId = completed.Id;
        anchor.UpdatedAt = now;
        anchor.CompletedAt ??= now;
        anchor.IsActive = false;
        anchor.ModifiedBy = Actor();
        anchor.ModifiedDate = now;
        _db.WorkflowHistory.Add(new WorkflowHistory
        {
            WorkflowInstanceId = anchor.Id,
            FromStatusId = fromStatusId,
            ToStatusId = completed.Id,
            Notes = "Approval decided",
            ChangedAt = now,
            CreatedBy = Actor(),
            CreatedDate = now
        });
    }

    private async Task<Status> SingleStatusAsync(string name, CancellationToken ct)
    {
        var lower = name.ToLower();
        var matches = await _db.Statuses.Where(s => s.IsActive && !s.IsDeleted && s.Name.ToLower() == lower).Take(2).ToListAsync(ct);
        if (matches.Count != 1) throw new BusinessRuleException($"Exactly one active '{name}' status is required.");
        return matches[0];
    }

    private void AddSameStatusHistory(WorkflowInstance anchor, string? notes, string transitionType, string actor, DateTime now) =>
        _db.WorkflowHistory.Add(new WorkflowHistory
        {
            WorkflowInstanceId = anchor.Id,
            FromStatusId = anchor.StatusId,
            ToStatusId = anchor.StatusId,
            Notes = notes,
            ChangedAt = now,
            CreatedBy = actor,
            CreatedDate = now,
            TransitionType = transitionType
        });

    private string Actor() => _user.UserName ?? _user.UserId.ToString(CultureInfo.InvariantCulture);

    // ============================================================
    // §7 — Business Approve/Reject must also freeze the currently-open phase and close the pause
    // anchor. Unlike Delegation (whose only path to Completed is via ApproveReviewAsync, which already
    // closes the open phase), Approval's PRIMARY completion path is this ordinary business decision —
    // most Approvals never touch submit-for-review/review/approve at all. Never called from
    // RequestChangesAsync/ResubmitAsync — those don't touch eaTask.ExecutionStatus, so the phase keeps running.
    // ============================================================

    private async Task FinalizePhaseAndAnchorAsync(long approvalRequestId, EaTask eaTask, DateTime now, CancellationToken ct)
    {
        var phase = await _db.ApprovalPhaseTats.FirstOrDefaultAsync(p => p.ApprovalRequestId == approvalRequestId && p.EndedAt == null, ct);
        if (phase is not null)
            await ClosePhaseAsync(phase, eaTask.WorkflowInstanceId, now, ct);
        if (eaTask.WorkflowInstanceId.HasValue)
            await CompleteAnchorAsync(eaTask.WorkflowInstanceId.Value, now, ct);
    }

    // ============================================================
    // PER-PHASE TAT (Actual / Review N / Rework N) — mirrors DelegationService's own region exactly,
    // just scoped to ApprovalPhaseTat/ApprovalRequestId and resolved via
    // ITatRuleRepository.GetApplicableForApprovalPhaseAsync instead of GetApplicableByTypeOnlyAsync.
    // ============================================================

    /// <summary>Soft lookup: null (no TAT for this phase) when nothing is configured, not an error.</summary>
    private async Task<(int? AllottedTatMinutes, long? TatRuleId)> ResolvePhaseTatAsync(long moduleId, string? requestType, string? department, string taskType, CancellationToken ct)
    {
        var applicable = await _tatRules.GetApplicableForApprovalPhaseAsync(moduleId, requestType, department, taskType, ct);
        return applicable.Count == 1 ? (applicable[0].TatMinutes, applicable[0].Id) : (null, null);
    }

    /// <summary>
    /// Opens (tracked, not yet saved) a new phase, resolving its own TAT rule fresh. Created idle —
    /// StartedAt null — its TAT clock only starts once StartActualAsync/StartReviewAsync/
    /// StartReworkAsync is explicitly called; see StartOpenPhaseAsync below.
    /// </summary>
    private async Task OpenPhaseAsync(long approvalRequestId, long moduleId, string? requestType, string? department, string taskType, int reviewCycleNumber, DateTime now, CancellationToken ct)
    {
        var (allotted, ruleId) = await ResolvePhaseTatAsync(moduleId, requestType, department, taskType, ct);
        var actor = Actor();
        _db.ApprovalPhaseTats.Add(new ApprovalPhaseTat
        {
            ApprovalRequestId = approvalRequestId, TaskType = taskType, ReviewCycleNumber = reviewCycleNumber,
            StartedAt = null, AllottedTatMinutes = allotted, TatRuleId = ruleId,
            CreatedBy = actor, CreatedDate = now
        });
    }

    /// <summary>
    /// Explicit Start for a phase opened idle (see OpenPhaseAsync/ApprovalService.CreateAsync) — sets
    /// StartedAt to now and begins TAT accrual. Shared by all three phase types (unlike Delegation,
    /// where only Review/Rework share this helper and Actual has its own bespoke StartAsync): Approval's
    /// Actual phase row already exists idle from creation time, so the same "find the open phase, assert
    /// its type and that it hasn't started yet" logic applies uniformly. Actual additionally flips the
    /// central EaTask from NotStarted to InProgress — the one place execution truly begins for an
    /// Approval — exactly like Delegation's own StartAsync does for its Actual phase. 409 if the current
    /// open phase is a different TaskType, was already started, or (for Review/Rework) the EaTask isn't
    /// InProgress yet.
    /// </summary>
    private async Task<ApprovalDetailDto> StartOpenPhaseAsync(long approvalRequestId, string taskType, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var req = await RequireApprovalAsync(approvalRequestId, ct);
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);

        if (taskType == DelegationTaskType.Actual)
        {
            if (eaTask.ExecutionStatus != EaTaskExecutionStatus.NotStarted)
                throw new BusinessRuleException($"Approval cannot be started from its current execution status '{eaTask.ExecutionStatus}'.");
        }
        else
        {
            RequireInProgress(eaTask, "started");
        }

        await RequireNoOpenPauseAsync(eaTask.WorkflowInstanceId, ct);

        var phase = await RequireOpenPhaseAsync(approvalRequestId, taskType, null, ct);
        if (phase.StartedAt.HasValue)
            throw new BusinessRuleException($"The current {taskType} phase has already been started.");

        var now = Clock.UtcNowTz;
        phase.StartedAt = now;
        phase.ModifiedBy = Actor();
        phase.ModifiedDate = now;

        if (taskType == DelegationTaskType.Actual)
        {
            eaTask.ExecutionStatus = EaTaskExecutionStatus.InProgress;
            eaTask.StartedAt = now;
        }

        req.UpdatedBy = Actor();
        req.UpdatedAt = now;

        _audit.AddAudit(
            $"APPROVAL_{taskType.ToUpperInvariant()}_START", "Approval", nameof(ApprovalRequest),
            req.Id.ToString(CultureInfo.InvariantCulture),
            new { TaskType = taskType, StartedAt = (DateTime?)null },
            new { TaskType = taskType, phase.ReviewCycleNumber, phase.StartedAt },
            $"Approval {taskType} phase started");

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await Query.DetailAsync(approvalRequestId, ct) ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
    }

    /// <summary>Starts the central EaTask and the Actual phase's own TAT clock (NotStarted -> InProgress). 409 if not currently NotStarted or already started.</summary>
    public Task<ApprovalDetailDto> StartActualAsync(long approvalRequestId, CancellationToken ct = default) =>
        StartOpenPhaseAsync(approvalRequestId, DelegationTaskType.Actual, ct);

    /// <summary>Starts the reviewer's own SLA clock for the currently open Review phase. 409 if the current open phase isn't a not-yet-started Review phase.</summary>
    public Task<ApprovalDetailDto> StartReviewAsync(long approvalRequestId, CancellationToken ct = default) =>
        StartOpenPhaseAsync(approvalRequestId, DelegationTaskType.Review, ct);

    /// <summary>Starts the doer's redo clock for the currently open Rework phase. 409 if the current open phase isn't a not-yet-started Rework phase.</summary>
    public Task<ApprovalDetailDto> StartReworkAsync(long approvalRequestId, CancellationToken ct = default) =>
        StartOpenPhaseAsync(approvalRequestId, DelegationTaskType.Rework, ct);

    private async Task RequireNoOpenPauseAsync(long? workflowInstanceId, CancellationToken ct)
    {
        if (workflowInstanceId.HasValue && await _db.WorkPauses.AnyAsync(p =>
            p.WorkflowInstanceId == workflowInstanceId && !p.IsDeleted && p.EndAt == null, ct))
            throw new BusinessRuleException("Resume or continue open pauses/waiting before a phase transition.");
    }

    private async Task<ApprovalPhaseTat> RequireReviewPhaseAsync(long approvalRequestId, long eaTaskId, CancellationToken ct)
    {
        var review = await _db.TaskReviews.AsNoTracking().Where(r => r.EaTaskId == eaTaskId)
            .OrderByDescending(r => r.ReviewCycleNo).FirstOrDefaultAsync(ct);
        if (review is null || review.ReviewStatus != TaskReviewStatus.PendingReview)
            throw new BusinessRuleException("A pending review is required for this transition.");
        return await RequireOpenPhaseAsync(approvalRequestId, DelegationTaskType.Review, review.ReviewCycleNo, ct);
    }

    private async Task<ApprovalPhaseTat> RequireOpenPhaseAsync(long approvalRequestId, string taskType, int? cycle, CancellationToken ct)
    {
        var phases = await _db.ApprovalPhaseTats.Where(p => p.ApprovalRequestId == approvalRequestId && p.EndedAt == null)
            .Take(2).ToListAsync(ct);
        if (phases.Count != 1 || phases[0].TaskType != taskType || (cycle.HasValue && phases[0].ReviewCycleNumber != cycle))
            throw new BusinessRuleException($"Approval phase data is inconsistent: expected exactly one open {taskType} phase for cycle {cycle}. Historical data requires explicit repair.");
        return phases[0];
    }

    private async Task ClosePhaseAsync(ApprovalPhaseTat phase, long? workflowInstanceId, DateTime now, CancellationToken ct)
    {
        phase.EndedAt = now;
        if (!phase.StartedAt.HasValue)
        {
            // Never explicitly started (Review/Rework can close without ever having their clock
            // started) — nothing accrued, so freeze a zero snapshot rather than dividing by a window
            // that never opened.
            phase.TatUsedMinutes = phase.AllottedTatMinutes.HasValue ? 0 : null;
            phase.TatPausedMinutes = 0;
            phase.TatUsedSeconds = phase.AllottedTatMinutes.HasValue ? 0m : null;
            phase.TatPausedSeconds = 0m;
            phase.PauseCount = 0;
        }
        else
        {
            var pauses = PausesOverlapping(await LoadApprovalWorkPausesAsync(workflowInstanceId, ct), phase.StartedAt.Value, now);
            var summary = TatSummaryCalculator.Calculate(phase.AllottedTatMinutes ?? 0, phase.StartedAt.Value, now, pauses, now);
            phase.TatUsedMinutes = phase.AllottedTatMinutes.HasValue ? (int)summary.Tat!.Value.TotalMinutes : null;
            phase.TatPausedMinutes = (int)summary.PauseTime.TotalMinutes;
            phase.TatUsedSeconds = phase.AllottedTatMinutes.HasValue ? DurationSeconds(summary.Tat!.Value) : null;
            phase.TatPausedSeconds = DurationSeconds(summary.PauseTime);
            phase.PauseCount = summary.PauseCount;
        }
        phase.ModifiedBy = Actor();
        phase.ModifiedDate = now;
        // Release the single-open-phase index entry before inserting the successor, within the same transaction.
        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<WorkPause>> LoadApprovalWorkPausesAsync(long? workflowInstanceId, CancellationToken ct) =>
        workflowInstanceId.HasValue
            ? await _db.WorkPauses.AsNoTracking().Where(p => p.WorkflowInstanceId == workflowInstanceId && !p.IsDeleted).ToListAsync(ct)
            : new List<WorkPause>();

    /// <summary>Simple pauses overlapping [start, windowEnd) — an open pause (EndAt null) always overlaps since it has no upper bound yet.</summary>
    private static List<WorkPause> PausesOverlapping(IReadOnlyCollection<WorkPause> pauses, DateTime start, DateTime windowEnd) =>
        pauses.Where(p => WorkPauseClassifier.IsSimplePause(p) && p.StartAt < windowEnd && (p.EndAt ?? DateTime.MaxValue) > start).ToList();

    // Decimal tick conversion avoids floating-point loss and retains TimeSpan's 100 ns precision.
    private static decimal DurationSeconds(TimeSpan duration) => duration.Ticks / (decimal)TimeSpan.TicksPerSecond;

    /// <summary>
    /// Same rules as Delegation's completion/review attachment validation (ReadValidatedPdfAsync):
    /// non-empty, at most 25 MiB, .pdf and application/pdf, %PDF- signature and a readable document
    /// with at least one page. Duplicated rather than shared — a 2-caller helper doesn't earn an abstraction.
    /// </summary>
    private static async Task<byte[]> ReadValidatedPdfAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0 || file.Length > MaxAttachmentBytes)
            throw new BusinessRuleException("A nonempty PDF attachment of at most 25 MiB is required.");
        var originalFileName = Path.GetFileName(file.FileName?.Replace('\\', '/') ?? string.Empty);
        if (string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Length > 500)
            throw new BusinessRuleException("PDF attachment filename must contain 1 to 500 characters.");
        if (!string.Equals(Path.GetExtension(originalFileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Attachment must be a PDF.");

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > MaxAttachmentBytes) throw new BusinessRuleException("PDF attachment exceeds 25 MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.ToArray();
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new BusinessRuleException("PDF attachment signature is invalid.");
        try { using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes); if (pdf.NumberOfPages < 1) throw new InvalidDataException(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { throw new BusinessRuleException("Attachment is not a readable PDF."); }
        return bytes;
    }

    /// <summary>Writes the PDF to Content/ApprovalReview/{approvalRequestId}/ and returns the relative object key. Removes a partial file on failure.</summary>
    private async Task<string> WriteApprovalAttachmentAsync(long approvalRequestId, byte[] content, CancellationToken ct)
    {
        var key = $"Content/ApprovalReview/{approvalRequestId.ToString(CultureInfo.InvariantCulture)}/{Guid.NewGuid():N}.pdf";
        var absolute = ResolveStoragePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        try
        {
            await using var stream = new FileStream(absolute, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await stream.WriteAsync(content, ct);
            await stream.FlushAsync(ct);
        }
        catch { TryDeleteFile(key); throw; }
        return key;
    }

    /// <summary>Resolves an object key under Content/ (path-traversal guarded).</summary>
    private string ResolveStoragePath(string objectKey)
    {
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Content")) + Path.DirectorySeparatorChar;
        var absolute = Path.GetFullPath(Path.Combine(_env.ContentRootPath, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid attachment storage key.");
        return absolute;
    }

    private void TryDeleteFile(string objectKey)
    {
        try { var path = ResolveStoragePath(objectKey); if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
    }
}
