using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Jarvis5.Services.EaFms;

public class WorkflowExecutionService : IWorkflowExecutionService
{
    private readonly EaFmsDbContext _db;
    private readonly IWorkflowService _workflow;
    private readonly IWorkflowRepository _workflowRepo;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly IMapper _mapper;

    public WorkflowExecutionService(
        EaFmsDbContext db,
        IWorkflowService workflow,
        IWorkflowRepository workflowRepo,
        ICurrentUserService user,
        IAuditService audit,
        IMapper mapper)
    {
        _db = db;
        _workflow = workflow;
        _workflowRepo = workflowRepo;
        _user = user;
        _audit = audit;
        _mapper = mapper;
    }

    private string Actor => _user.UserName ?? _user.UserId.ToString();

    private async Task<IDbContextTransaction?> BeginTxIfNeededAsync(CancellationToken ct) =>
        _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

    private async Task<WorkflowInstance> LockAsync(long id, CancellationToken ct)
    {
        var rows = await _db.WorkflowInstances.FromSqlInterpolated(
            $"SELECT * FROM public.ea_workflow_instances WHERE \"Id\" = {id} FOR UPDATE")
            .ToListAsync(ct);
        var wf = rows.SingleOrDefault();
        if (wf is null || wf.IsDeleted) throw new NotFoundException($"Workflow {id} not found.");
        var status = await _db.Statuses.FindAsync(new object[] { wf.StatusId }, ct);
        if (!wf.IsActive || string.Equals(status?.Name, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status?.Name, "Archived", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Workflow is completed, archived or inactive.");
        return wf;
    }

    private async Task<int> StatusAsync(string name, CancellationToken ct)
    {
        var matches = await _db.Statuses.Where(s => s.IsActive && !s.IsDeleted
            && EF.Functions.ILike(s.Name, name)).Take(2).ToListAsync(ct);
        if (matches.Count != 1) throw new BusinessRuleException($"Exactly one active '{name}' status is required.");
        return matches[0].Id;
    }

    private async Task NoOpenOperationalStopAsync(long id, CancellationToken ct)
    {
        if (await _db.WorkPauses.AnyAsync(p => p.WorkflowInstanceId == id && !p.IsDeleted && p.EndAt == null, ct))
            throw new BusinessRuleException("An open pause or waiting period exists. Resume or continue it first.");
    }

    private Task<WorkflowResponseDto> ChangeAsync(long id, int status, string? notes, CancellationToken ct, bool preserveStart = false)
        => _workflow.TransitionAsync(id, new TransitionWorkflowRequestDto { TargetStatusId = status, Notes = notes }, ct, preserveStart);

    private async Task AddSameStatusHistoryAsync(WorkflowInstance wf, string? notes, string transitionType, CancellationToken ct)
    {
        var now = Clock.UtcNowTz;
        await _workflowRepo.AddHistoryAsync(new WorkflowHistory
        {
            WorkflowInstanceId = wf.Id,
            FromStatusId = wf.StatusId,
            ToStatusId = wf.StatusId,
            Notes = notes,
            ChangedAt = now,
            CreatedBy = Actor,
            CreatedDate = now,
            TransitionType = transitionType
        }, ct);
    }

    public async Task<WorkflowResponseDto> StartAsync(long workflowId, StartWorkRequestDto dto, CancellationToken ct)
    {
        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            var wf = await LockAsync(workflowId, ct);
            await NoOpenOperationalStopAsync(workflowId, ct);
            var name = (await _db.Statuses.FindAsync(new object[] { wf.StatusId }, ct))?.Name;
            if (!string.Equals(name, "Captured", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "In Progress", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Start requires Captured or In Progress state.");
            var result = await ChangeAsync(workflowId, await StatusAsync("In Progress", ct), dto.Notes, ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<SimplePauseResponseDto> PauseAsync(long workflowId, SimplePauseRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Remark))
            throw new BusinessRuleException("Pause remark is required.");

        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            var wf = await LockAsync(workflowId, ct);
            await NoOpenOperationalStopAsync(workflowId, ct);
            var name = (await _db.Statuses.FindAsync(new object[] { wf.StatusId }, ct))?.Name;
            if ((!string.Equals(name, "In Progress", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Submitted", StringComparison.OrdinalIgnoreCase)) || !wf.TatStartedAt.HasValue)
                throw new BusinessRuleException("Start execution before Simple Pause.");

            var now = Clock.UtcNowTz;
            var pause = new WorkPause
            {
                WorkflowInstanceId = wf.Id,
                IntakeRequestId = wf.IntakeRequestId,
                FollowupId = null,
                StartAt = now,
                Reason = dto.Remark.Trim(),
                CreatedBy = Actor,
                CreatedDate = now
            };
            _db.WorkPauses.Add(pause);
            await _db.SaveChangesAsync(ct);
            await AddSameStatusHistoryAsync(wf, pause.Reason, "SIMPLE_PAUSE", ct);
            _audit.AddAudit("PAUSE_START", "Workflow", nameof(WorkPause), pause.Id.ToString(), null,
                new { pause.WorkflowInstanceId, pause.StartAt, pause.Reason, Kind = "SimplePause" }, "Simple pause started");
            await _db.SaveChangesAsync(ct);

            var workflow = await _workflow.GetByIdAsync(wf.Id, ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return new SimplePauseResponseDto
            {
                PauseId = pause.Id,
                Remark = pause.Reason,
                StartAt = pause.StartAt,
                EndAt = pause.EndAt,
                Workflow = workflow
            };
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<SimplePauseResponseDto> ResumePauseAsync(long workflowId, SimpleResumeRequestDto dto, CancellationToken ct)
    {
        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            var wf = await LockAsync(workflowId, ct);
            var open = await _db.WorkPauses
                .Where(p => p.WorkflowInstanceId == workflowId && !p.IsDeleted && p.EndAt == null)
                .OrderByDescending(p => p.StartAt).ThenByDescending(p => p.Id)
                .ToListAsync(ct);
            if (open.Count == 0)
                throw new BusinessRuleException("No open Simple Pause found.");
            if (open.Count > 1)
                throw new BusinessRuleException("Multiple open operational stops exist; resolve manually.");

            var pause = open[0];
            if (WorkPauseClassifier.IsDependencyWaiting(pause))
                throw new BusinessRuleException("Open record is Waiting/Follow-up, not Simple Pause. Use Continue Waiting.");

            var now = Clock.UtcNowTz;
            pause.EndAt = now;
            pause.ResumedById = _user.UserId.ToString();
            pause.ResumedByName = _user.UserName;
            pause.ResumedReason = string.IsNullOrWhiteSpace(dto.Remark) ? null : dto.Remark.Trim();
            pause.ModifiedBy = Actor;
            pause.ModifiedDate = now;

            await AddSameStatusHistoryAsync(wf, pause.ResumedReason ?? "Work resumed", "SIMPLE_RESUME", ct);
            _audit.AddAudit("PAUSE_RESUME", "Workflow", nameof(WorkPause), pause.Id.ToString(), null,
                new { pause.WorkflowInstanceId, pause.EndAt, pause.ResumedById, pause.ResumedByName, pause.ResumedReason, Kind = "SimplePause" },
                "Simple pause resumed");

            var currentName = (await _db.Statuses.FindAsync(new object[] { wf.StatusId }, ct))?.Name;
            WorkflowResponseDto workflow;
            if (!string.Equals(currentName, "In Progress", StringComparison.OrdinalIgnoreCase))
                workflow = await ChangeAsync(wf.Id, await StatusAsync("In Progress", ct), pause.ResumedReason ?? "Work resumed", ct, preserveStart: true);
            else
            {
                await _db.SaveChangesAsync(ct);
                workflow = await _workflow.GetByIdAsync(wf.Id, ct);
            }

            if (tx is not null) await tx.CommitAsync(ct);
            return new SimplePauseResponseDto
            {
                PauseId = pause.Id,
                Remark = pause.Reason,
                StartAt = pause.StartAt,
                EndAt = pause.EndAt,
                Workflow = workflow
            };
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<WorkflowWaitingResponseDto> WaitAsync(long workflowId, CreateWorkPauseRequestDto dto, CancellationToken ct)
    {
        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            var wf = await LockAsync(workflowId, ct);
            await NoOpenOperationalStopAsync(workflowId, ct);
            var name = (await _db.Statuses.FindAsync(new object[] { wf.StatusId }, ct))?.Name;
            if ((!string.Equals(name, "In Progress", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Submitted", StringComparison.OrdinalIgnoreCase)) || !wf.TatStartedAt.HasValue)
                throw new BusinessRuleException("Start execution before entering Waiting.");
            if (!WorkPauseClassifier.HasWaitingDependency(dto.WaitingOnId, dto.WaitingOnName, dto.WaitingOnExternal))
                throw new BusinessRuleException("Waiting requires who/what the work is waiting on.");
            var waitingStatus = await StatusAsync("Waiting / Follow-up", ct);
            if (!dto.RequestSentAt.HasValue) throw new BusinessRuleException("RequestSentAt is required.");
            var now = Clock.UtcNowTz;
            var request = dto as CreateWaitingRequestDto;
            Followup? followup = null;
            if (request != null)
            {
                if (!request.ExpectedResponseAt.HasValue || !request.NextFollowupAt.HasValue)
                    throw new BusinessRuleException("Expected response and next follow-up dates are required.");
                followup = new Followup
                {
                    WorkflowInstanceId = wf.Id, IntakeRequestId = wf.IntakeRequestId,
                    BusinessModuleId = wf.BusinessModuleId, BusinessRecordId = wf.BusinessRecordId,
                    DueAt = request.ExpectedResponseAt.Value, ExpectedResponseAt = request.ExpectedResponseAt,
                    NextFollowupAt = request.NextFollowupAt, Note = request.CurrentRemarks,
                    WaitingOnId = dto.WaitingOnId, WaitingOnName = dto.WaitingOnName,
                    WaitingOnExternal = dto.WaitingOnExternal, ResponseOwnerId = dto.ResponseOwnerId,
                    ResponseOwnerName = dto.ResponseOwnerName, CreatedBy = Actor, CreatedDate = now
                };
                _db.Followups.Add(followup);
                await _db.SaveChangesAsync(ct);
                _audit.AddAudit("FOLLOWUP_CREATE", "Followup", nameof(Followup), followup.Id.ToString(), null,
                    new { followup.WorkflowInstanceId, followup.NextFollowupAt, followup.ExpectedResponseAt, followup.Note }, "Waiting follow-up scheduled");
            }
            var pause = new WorkPause
            {
                WorkflowInstanceId = wf.Id, IntakeRequestId = wf.IntakeRequestId, FollowupId = followup?.Id,
                StartAt = now, Reason = dto.Reason, WaitingOnId = dto.WaitingOnId,
                WaitingOnName = dto.WaitingOnName, WaitingOnExternal = dto.WaitingOnExternal,
                ResponseOwnerId = dto.ResponseOwnerId, ResponseOwnerName = dto.ResponseOwnerName,
                RequestSentAt = dto.RequestSentAt, ExpectedResponseAt = dto.ExpectedResponseAt,
                CreatedBy = Actor, CreatedDate = now
            };
            _db.WorkPauses.Add(pause);
            await _db.SaveChangesAsync(ct);
            _audit.AddAudit("PAUSE_START", "Workflow", nameof(WorkPause), pause.Id.ToString(), null,
                new { pause.WorkflowInstanceId, pause.FollowupId, pause.StartAt, pause.RequestSentAt,
                    pause.Reason, pause.WaitingOnId, pause.WaitingOnName, pause.WaitingOnExternal,
                    pause.ResponseOwnerId, pause.ResponseOwnerName, pause.ExpectedResponseAt, Kind = "Waiting" }, "Workflow waiting");
            await ChangeAsync(wf.Id, waitingStatus, dto.Reason, ct, true);
            var result = await GetWaitingAsync(wf.Id, pause.Id, ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<WorkflowWaitingResponseDto> ContinueAsync(long workflowId, long waitingId, ResumeWorkPauseRequestDto dto, CancellationToken ct)
    {
        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            var wf = await LockAsync(workflowId, ct);
            var pause = await _db.WorkPauses.SingleOrDefaultAsync(p => p.Id == waitingId && p.WorkflowInstanceId == workflowId && !p.IsDeleted, ct)
                ?? throw new NotFoundException("Waiting record does not exist for this workflow.");
            if (pause.EndAt.HasValue) throw new BusinessRuleException("Waiting record is already closed.");
            if (!WorkPauseClassifier.IsDependencyWaiting(pause))
                throw new BusinessRuleException("Record is a Simple Pause, not Waiting/Follow-up. Use Resume.");
            var current = (await _db.Statuses.FindAsync(new object[] { wf.StatusId }, ct))?.Name;
            if (!string.Equals(current, "Waiting / Follow-up", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Workflow must be in Waiting / Follow-up.");
            var target = dto.TargetStatusName?.Trim();
            if (!string.Equals(target, "In Progress", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(target, "Submitted", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Continue target must be In Progress or Submitted.");
            var targetId = await StatusAsync(target!, ct);
            var now = Clock.UtcNowTz;
            if (pause.FollowupId.HasValue)
            {
                var linked = await _db.Followups.FromSqlInterpolated(
                    $"SELECT * FROM public.ea_followups WHERE \"Id\" = {pause.FollowupId.Value} FOR UPDATE").ToListAsync(ct);
                var followup = linked.SingleOrDefault();
                if (followup is null || followup.IsDeleted || followup.WorkflowInstanceId != wf.Id
                    || followup.BusinessModuleId != wf.BusinessModuleId || followup.BusinessRecordId != wf.BusinessRecordId)
                    throw new BusinessRuleException("Waiting follow-up source is inconsistent with this workflow.");
                if (!followup.CompletedAt.HasValue)
                {
                    followup.CompletedAt = now; followup.CompletionNote = dto.ResumedReason;
                    followup.CompletedById = _user.UserId.ToString(); followup.CompletedByName = _user.UserName;
                    followup.ModifiedBy = Actor; followup.ModifiedDate = now;
                    _audit.AddAudit("FOLLOWUP_COMPLETE", "Followup", nameof(Followup), followup.Id.ToString(), null,
                        new { followup.WorkflowInstanceId, followup.CompletedAt, followup.CompletionNote }, "Waiting dependency resolved");
                }
            }
            pause.EndAt = now; pause.ResumedById = _user.UserId.ToString(); pause.ResumedByName = _user.UserName;
            pause.ResumedReason = dto.ResumedReason; pause.ModifiedBy = Actor; pause.ModifiedDate = now;
            _audit.AddAudit("PAUSE_RESUME", "Workflow", nameof(WorkPause), pause.Id.ToString(), null,
                new { pause.WorkflowInstanceId, pause.EndAt, pause.ResumedById, pause.ResumedByName, pause.ResumedReason, TargetStatusName = target, Kind = "Waiting" }, "Workflow continued");
            await ChangeAsync(wf.Id, targetId, dto.ResumedReason, ct, true);
            var result = await GetWaitingAsync(wf.Id, pause.Id, ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<CompleteWorkResponseDto> CompleteAsync(long workflowId, CompleteWorkRequestDto dto, CancellationToken ct)
    {
        await using var tx = await BeginTxIfNeededAsync(ct);
        try
        {
            var wf = await LockAsync(workflowId, ct);
            await NoOpenOperationalStopAsync(workflowId, ct);
            var name = (await _db.Statuses.FindAsync(new object[] { wf.StatusId }, ct))?.Name;
            if ((!string.Equals(name, "In Progress", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "Submitted", StringComparison.OrdinalIgnoreCase)) || !wf.TatStartedAt.HasValue)
                throw new BusinessRuleException("Complete requires started work in In Progress or Submitted state.");
            if (dto.EvidenceAttachmentIds.Count != 0)
                throw new BusinessRuleException("Completion evidence is not configured: attachment ownership and persisted closure-evidence role must be defined before evidence IDs can be accepted. Workflow was not completed.");
            var result = await ChangeAsync(wf.Id, await StatusAsync("Completed", ct), dto.Notes, ct, true);
            if (tx is not null) await tx.CommitAsync(ct);
            return new CompleteWorkResponseDto { Workflow = result };
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<WorkflowWaitingListResponseDto> GetWaitingAsync(long workflowId, CancellationToken ct)
    {
        var wf = await _workflow.GetByIdAsync(workflowId, ct);
        var pauses = await _db.WorkPauses.AsNoTracking().Where(p => p.WorkflowInstanceId == workflowId && !p.IsDeleted)
            .OrderByDescending(p => p.StartAt).ThenByDescending(p => p.Id).ToListAsync(ct);
        var items = new List<WaitingResponseDto>();
        foreach (var p in pauses.Where(WorkPauseClassifier.IsDependencyWaiting))
            items.Add(await MapAsync(p, wf, ct));
        return new WorkflowWaitingListResponseDto { Workflow = wf, Items = items };
    }

    public async Task<WorkflowWaitingResponseDto> GetWaitingAsync(long workflowId, long waitingId, CancellationToken ct)
    {
        var wf = await _workflow.GetByIdAsync(workflowId, ct);
        var pause = await _db.WorkPauses.AsNoTracking().SingleOrDefaultAsync(p => p.Id == waitingId && p.WorkflowInstanceId == workflowId && !p.IsDeleted, ct)
            ?? throw new NotFoundException("Waiting record does not exist for this workflow.");
        if (!WorkPauseClassifier.IsDependencyWaiting(pause))
            throw new NotFoundException("Waiting record does not exist for this workflow.");
        return new WorkflowWaitingResponseDto { Workflow = wf, Waiting = await MapAsync(pause, wf, ct) };
    }

    private async Task<WaitingResponseDto> MapAsync(WorkPause pause, WorkflowResponseDto wf, CancellationToken ct)
    {
        var result = _mapper.Map<WaitingResponseDto>(pause);
        result.WorkflowStatusId = wf.StatusId; result.WorkflowStatusName = wf.StatusName;
        if (pause.FollowupId.HasValue)
        {
            var f = await _db.Followups.AsNoTracking().SingleOrDefaultAsync(f => f.Id == pause.FollowupId && !f.IsDeleted
                && f.WorkflowInstanceId == wf.Id && f.BusinessModuleId == wf.BusinessModuleId && f.BusinessRecordId == wf.BusinessRecordId, ct);
            if (f != null)
            {
                result.LastFollowupAt = f.LastFollowupAt; result.NextFollowupAt = f.NextFollowupAt; result.CurrentRemarks = f.Note;
                var cycle = await _db.FollowupCycles.AsNoTracking().Where(c => c.FollowupId == f.Id)
                    .OrderByDescending(c => c.FollowedUpAt).ThenByDescending(c => c.SequenceNumber).FirstOrDefaultAsync(ct);
                if (cycle != null && (!f.LastFollowupAt.HasValue || cycle.FollowedUpAt > f.LastFollowupAt))
                {
                    result.LastFollowupAt = cycle.FollowedUpAt;
                    if (!f.ModifiedDate.HasValue || cycle.FollowedUpAt >= f.ModifiedDate)
                    { result.NextFollowupAt = cycle.NextFollowupAt ?? f.NextFollowupAt; result.CurrentRemarks = cycle.Note ?? f.Note; }
                }
            }
        }
        return result;
    }

    public async Task<PauseSummaryResponseDto> GetSummaryAsync(long workflowId, CancellationToken ct)
    {
        var wf = await _workflow.GetByIdAsync(workflowId, ct);
        var pauses = await _db.WorkPauses.AsNoTracking().Where(p => p.WorkflowInstanceId == workflowId && !p.IsDeleted)
            .OrderByDescending(p => p.StartAt).ThenByDescending(p => p.Id).ToListAsync(ct);
        var current = pauses.FirstOrDefault(p => p.EndAt == null);
        var isWaiting = current != null && WorkPauseClassifier.IsDependencyWaiting(current);
        var summary = new PauseSummaryResponseDto
        {
            WorkflowInstanceId = workflowId,
            IsPaused = current != null,
            PauseCount = pauses.Count,
            CurrentPauseId = current?.Id,
            CurrentPauseStartedAt = current?.StartAt,
            CurrentPauseReason = current?.Reason,
            WaitingOnId = isWaiting ? current!.WaitingOnId : null,
            WaitingOnName = isWaiting ? current!.WaitingOnName : null,
            WaitingOnExternal = isWaiting ? current!.WaitingOnExternal : null,
            ResponseOwnerId = isWaiting ? current!.ResponseOwnerId : null,
            ResponseOwnerName = isWaiting ? current!.ResponseOwnerName : null,
            ExpectedResponseAt = isWaiting ? current!.ExpectedResponseAt : null,
            RequestSentAt = isWaiting ? current!.RequestSentAt : null
        };
        if (wf.TatStartedAt is DateTime start)
        {
            var end = wf.CompletedAt ?? Clock.UtcNowTz;
            summary.TotalPausedMinutes = pauses.Sum(p =>
            {
                var a = p.StartAt > start ? p.StartAt : start;
                var b = (p.EndAt ?? end) < end ? (p.EndAt ?? end) : end;
                return b > a ? (int)(b - a).TotalMinutes : 0;
            });
            summary.TatPausedMinutes = summary.TotalPausedMinutes;
            summary.TatUsedMinutes = Math.Max(0, (int)(end - start).TotalMinutes - summary.TotalPausedMinutes);
        }
        return summary;
    }
}
