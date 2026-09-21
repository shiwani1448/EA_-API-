using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Module facade: MeetingId → linked WorkflowInstance → shared lifecycle engine.
/// Does not duplicate Start/Pause/Resume/Waiting/Complete business rules.
/// </summary>
public class MeetingLifecycleService : IMeetingLifecycleService
{
    private readonly EaFmsDbContext _db;
    private readonly IWorkflowExecutionService _execution;
    private readonly ICurrentUserService _user;
    private readonly IAuditService _audit;
    private readonly IMeetingService _meetings;
    private readonly IMeetingCompletionFileStore _files;
    private readonly DelegationService _delegations;
    private readonly ILogger<MeetingLifecycleService> _logger;

    public MeetingLifecycleService(
        EaFmsDbContext db,
        IWorkflowExecutionService execution,
        ICurrentUserService user,
        IAuditService audit, IMeetingService meetings, IMeetingCompletionFileStore files,
        DelegationService delegations,
        ILogger<MeetingLifecycleService> logger)
    {
        _db = db;
        _execution = execution;
        _user = user;
        _audit = audit;
        _meetings = meetings;
        _files = files;
        _delegations = delegations;
        _logger = logger;
    }

    private string Actor => _user.UserName ?? _user.UserId.ToString();

    private async Task<(Meeting Meeting, long WorkflowId)> RequireLinkedWorkflowAsync(long meetingId, CancellationToken ct)
    {
        var meeting = await _db.Meetings.SingleOrDefaultAsync(m => m.Id == meetingId && !m.IsDeleted, ct)
            ?? throw new NotFoundException($"Meeting {meetingId} not found.");
        if (!meeting.WorkflowInstanceId.HasValue)
            throw new BusinessRuleException("Meeting has no workflow instance.");

        var workflowId = meeting.WorkflowInstanceId.Value;
        var wf = await _db.WorkflowInstances.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == workflowId && !w.IsDeleted, ct)
            ?? throw new NotFoundException($"Workflow {workflowId} not found.");

        var recordId = meeting.Id.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(wf.BusinessRecordId, recordId, StringComparison.Ordinal)
            || !wf.BusinessModuleId.HasValue)
            throw new BusinessRuleException("Workflow instance does not belong to this Meeting.");

        var moduleOk = await _db.BusinessModules.AnyAsync(b =>
            b.Id == wf.BusinessModuleId.Value && b.IsActive && !b.IsDeleted
            && b.Name.Trim().ToLower() == "meeting", ct);
        if (!moduleOk)
            throw new BusinessRuleException("Workflow instance does not belong to this Meeting.");

        return (meeting, workflowId);
    }

    private static MeetingLifecycleResponseDto MapLifecycle(long meetingId, Meeting? meeting, WorkflowResponseDto wf, string? notes = null) =>
        new()
        {
            MeetingId = meetingId,            StatusName = wf.StatusName,
            IsPaused = false,
            ExecutionState = MeetingExecutionStateMapper.Map(wf.TatStartedAt.HasValue, wf.CompletedAt.HasValue),
            StartedAt = wf.TatStartedAt,
            TatSummary = new MeetingTatSummaryDto { Tat = null, TotalTat = TimeSpan.Zero, StartTime = wf.TatStartedAt, EndTime = wf.CompletedAt, LastActiveTime = wf.CompletedAt ?? wf.TatStartedAt, PauseTime = TimeSpan.Zero, PauseCount = 0 },
            CompletedAt = wf.CompletedAt,
            MeetingCompletedAt = meeting?.CompletedAt,
            IsActive = wf.IsActive,
            Notes = notes
        };

    private static MeetingPauseResponseDto MapPause(long meetingId, SimplePauseResponseDto pause) =>
        new()
        {
            MeetingId = meetingId,
            PauseId = pause.PauseId,
            Remark = pause.Remark,
            StartAt = pause.StartAt,
            EndAt = pause.EndAt,
            StatusName = pause.Workflow.StatusName,
            IsPaused = !pause.EndAt.HasValue,
            ExecutionState = MeetingExecutionStateMapper.Map(pause.Workflow.TatStartedAt.HasValue, pause.Workflow.CompletedAt.HasValue),
            StartedAt = pause.Workflow.TatStartedAt,
            TatSummary = new MeetingTatSummaryDto { Tat = null, TotalTat = TimeSpan.Zero, StartTime = pause.Workflow.TatStartedAt, EndTime = pause.Workflow.CompletedAt, LastActiveTime = pause.EndAt.HasValue ? pause.EndAt : pause.StartAt, PauseTime = pause.EndAt.HasValue ? pause.EndAt.Value - pause.StartAt : Clock.UtcNowTz - pause.StartAt, PauseCount = 1 },
            CompletedAt = pause.Workflow.CompletedAt,
            IsActive = pause.Workflow.IsActive
        };

    private async Task<MeetingLifecycleResponseDto> EnrichAsync(MeetingLifecycleResponseDto response, CancellationToken ct)
    {
        var context = await _meetings.GetByIdAsync(response.MeetingId, ct);
        response.Type = context.Type; response.Subtype = context.Subtype; response.Doers = context.Doers;
        response.AssignmentSummary = context.AssignmentSummary;
        response.TatSummary = context.TatSummary!;
        response.MeetingCompletedAt = context.CompletedAt;
        response.CompletionMom = context.CompletionMom;
        response.CompletionPdfAttachmentId = context.CompletionPdfAttachmentId;
        return response;
    }
    private async Task<MeetingPauseResponseDto> EnrichAsync(MeetingPauseResponseDto response, CancellationToken ct)
    {
        var context = await _meetings.GetByIdAsync(response.MeetingId, ct);
        response.Type = context.Type; response.Subtype = context.Subtype; response.Doers = context.Doers;
        response.AssignmentSummary = context.AssignmentSummary;
        response.TatSummary = context.TatSummary!;
        return response;
    }

    public async Task<MeetingLifecycleResponseDto> StartAsync(long meetingId, MeetingStartRequestDto dto, CancellationToken ct)
    {
        // Own the transaction so the shared execution engine joins it (it begins its own
        // only when none is ambient) and the EaTask sync below commits atomically with it —
        // no second HTTP round trip, no separate write outside this call.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var (meeting, workflowId) = await RequireLinkedWorkflowAsync(meetingId, ct);
            var wf = await _execution.StartAsync(workflowId, new StartWorkRequestDto(), ct);
            var tasks = await _db.Tasks.Include(x => x.BusinessModule)
                .Where(x => x.BusinessRecordId == meetingId.ToString(CultureInfo.InvariantCulture) && !x.IsDeleted
                    && x.BusinessModule.Name.Trim().ToLower() == "meeting")
                .Take(2).ToListAsync(ct);
            if (tasks.Count > 1) throw new BusinessRuleException("Meeting has ambiguous EA task snapshots.");
            if (tasks.Count == 1 && wf.TatStartedAt.HasValue)
            {
                tasks[0].ExecutionStatus = EaTaskExecutionStatus.InProgress;
                tasks[0].StartedAt = wf.TatStartedAt;
                await _db.SaveChangesAsync(ct);
            }
            var result = await EnrichAsync(MapLifecycle(meetingId, null, wf), ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<MeetingPauseResponseDto> PauseAsync(long meetingId, MeetingPauseRequestDto dto, CancellationToken ct)
    {
        var (_, workflowId) = await RequireLinkedWorkflowAsync(meetingId, ct);
        var pause = await _execution.PauseAsync(workflowId, new SimplePauseRequestDto { Remark = "Meeting paused" }, ct);
        return await EnrichAsync(MapPause(meetingId, pause), ct);
    }

    public async Task<MeetingPauseResponseDto> ResumeAsync(long meetingId, MeetingResumeRequestDto dto, CancellationToken ct)
    {
        var (_, workflowId) = await RequireLinkedWorkflowAsync(meetingId, ct);
        var pause = await _execution.ResumePauseAsync(workflowId, new SimpleResumeRequestDto(), ct);
        return await EnrichAsync(MapPause(meetingId, pause), ct);
    }

    public async Task<MeetingLifecycleResponseDto> CompleteAsync(long meetingId, MeetingCompleteRequestDto dto, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        string? objectKey = null;
        var commitAttempted = false;
        MeetingLifecycleResponseDto response;
        try
        {
            var (meeting, workflowId) = await RequireLinkedWorkflowAsync(meetingId, ct);
            // Same workflow lock as the shared engine; hold through evidence and completion.
            var workflows = await _db.WorkflowInstances.FromSqlInterpolated(
                $"SELECT * FROM public.ea_workflow_instances WHERE \"Id\" = {workflowId} FOR UPDATE").ToListAsync(ct);
            var wf = workflows.Single();
            await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.ea_meetings WHERE \"Id\" = {meetingId} FOR UPDATE", ct);
            await _db.Entry(meeting).ReloadAsync(ct);
            if (meeting.IsDeleted || meeting.WorkflowInstanceId != workflowId || meeting.CompletedAt.HasValue || meeting.ArchivedAt.HasValue || !wf.IsActive || wf.IsDeleted)
                throw new BusinessRuleException("Meeting is completed, archived, inactive or no longer linked to this workflow.");
            var status = await _db.Statuses.Where(x => x.Id == wf.StatusId).Select(x => x.Name).SingleAsync(ct);
            if (!wf.TatStartedAt.HasValue || !(string.Equals(status, "In Progress", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase)))
                throw new BusinessRuleException("Complete requires started work in In Progress or Submitted state.");
            if (await _db.WorkPauses.AnyAsync(x => x.WorkflowInstanceId == workflowId && !x.IsDeleted && x.EndAt == null, ct))
                throw new BusinessRuleException("Resume or continue open pauses/waiting before completion.");
            if (string.IsNullOrWhiteSpace(dto.CompletionMom) || dto.CompletionMom.Length > 4000)
                throw new BusinessRuleException("Completion MOM must contain 1 to 4000 characters.");
            var content = await _files.ValidateAsync(dto.CompletionPdf, ct);
            var tasks = await _db.Tasks.Where(x => x.BusinessModuleId == wf.BusinessModuleId && x.BusinessRecordId == meeting.Id.ToString(CultureInfo.InvariantCulture) && !x.IsDeleted).Take(2).ToListAsync(ct);
            if (tasks.Count > 1) throw new BusinessRuleException("Meeting has ambiguous EA task snapshots.");

            // Meeting -> Doer Delegation (Step 5B-3): every persisted, non-deleted
            // MeetingAction becomes its own real Delegation owned by the same Doer,
            // created inside this same transaction so a missing-doer failure (or any other
            // failure) rolls back the whole Meeting completion — validated and created
            // before the PDF is written to storage below.
            await CreateDelegationsForMeetingActionsAsync(meeting, ct);

            objectKey = await _files.SaveAsync(meeting.Id, content, ct);
            var now = Clock.UtcNowTz;
            var attachment = new Attachment
            {
                RelatedModule = "Meeting", RelatedEntity = "Meeting",
                RelatedEntityId = meeting.Id.ToString(CultureInfo.InvariantCulture),
                OriginalFileName = Path.GetFileName(dto.CompletionPdf.FileName),
                ObjectKey = objectKey, ContentType = "application/pdf", Size = content.LongLength,
                AccessUrl = "/" + objectKey, Metadata = "{\"purpose\":\"MeetingCompletionPdf\"}",
                UploadedBy = Actor, UploadedAt = now, CreatedBy = Actor, CreatedDate = now, IsActive = true
            };
            _db.Attachments.Add(attachment);
            meeting.CompletionMom = dto.CompletionMom.Trim();
            meeting.CompletionPdfAttachment = attachment;
            meeting.ModifiedBy = Actor; meeting.ModifiedDate = now;
            await _db.SaveChangesAsync(ct);

            // Evidence is owned/validated by the Meeting facade and its explicit FK.
            // Preserve the shared engine's prohibition on generic evidence IDs.
            var completed = await _execution.CompleteAsync(workflowId, new CompleteWorkRequestDto(), ct);
            meeting.CompletedAt = completed.Workflow.CompletedAt ?? now;
            if (tasks.Count == 1)
            {
                tasks[0].IsActive = false; tasks[0].ModifiedBy = Actor; tasks[0].ModifiedDate = now;
                tasks[0].ExecutionStatus = EaTaskExecutionStatus.Completed;
                tasks[0].CompletedAt ??= meeting.CompletedAt;
                // Freeze actual TAT used, once, using the same canonical elapsed-minus-paused
                // calculation already used for Meeting display (WorkPauseClassifier). wf.TatStartedAt
                // is guaranteed set here by the completion precondition checked above.
                if (tasks[0].AllottedTatMinutes.HasValue && wf.TatStartedAt.HasValue)
                {
                    var pausesForTat = await _db.WorkPauses.AsNoTracking()
                        .Where(p => p.WorkflowInstanceId == workflowId && !p.IsDeleted).ToListAsync(ct);
                    tasks[0].TatUsedMinutes = EaTaskService.CalculateActiveTatMinutes(
                        wf.TatStartedAt.Value, meeting.CompletedAt.Value, pausesForTat);
                }
            }
            _audit.AddAudit("MEETING_COMPLETE", "Meeting", nameof(Meeting), meeting.Id.ToString(CultureInfo.InvariantCulture), null,
                new { meeting.Id, meeting.CompletedAt, meeting.CompletionPdfAttachmentId }, "Meeting completed with independent evidence");
            await _db.SaveChangesAsync(ct);
            response = await EnrichAsync(MapLifecycle(meetingId, meeting, completed.Workflow), ct);
            commitAttempted = true;
            await tx.CommitAsync(ct);
        }
        catch
        {
            var rolledBack = false;
            try { await tx.RollbackAsync(CancellationToken.None); rolledBack = true; }
            catch (Exception rollbackError) { _logger.LogError(rollbackError, "Meeting completion rollback failed for {MeetingId}", meetingId); }
            // A lost connection during commit has an uncertain outcome: retain the file for reconciliation.
            if (objectKey is not null && rolledBack && !commitAttempted)
            {
                try { _files.Delete(objectKey); }
                catch (Exception cleanupError) { _logger.LogError(cleanupError, "Orphan completion file requires cleanup: {ObjectKey}", objectKey); }
            }
            else if (objectKey is not null) _logger.LogError("Completion file requires commit reconciliation: {ObjectKey}", objectKey);
            _db.ChangeTracker.Clear();
            throw;
        }
        return response;
    }

    /// <summary>
    /// Meeting -> Doer Delegation (Step 5B-3). Converts every persisted, non-deleted
    /// MeetingAction belonging to this Meeting into its own real Delegation owned by
    /// the same Doer (MeetingAction.DoerId/DoerName copied verbatim — never
    /// fabricated, never the current EA actor). Reuses
    /// DelegationService.CreateCoreAsync, which is transaction-composable (never begins or
    /// commits its own transaction), so every Delegation this creates joins the caller's
    /// already-open Meeting-completion transaction: any failure — including a missing
    /// Doer on a later action — rolls back everything, including actions already
    /// converted earlier in this same call. Zero actions is valid and creates zero
    /// Delegations. Deleted MeetingActions are excluded by the query itself.
    /// </summary>
    private async Task CreateDelegationsForMeetingActionsAsync(Meeting meeting, CancellationToken ct)
    {
        var actions = await _db.MeetingActions
            .Where(a => a.MeetingId == meeting.Id && !a.IsDeleted)
            .ToListAsync(ct);
        if (actions.Count == 0) return;

        // Validate every action BEFORE creating any Delegation. A single missing Doer
        // fails the whole Meeting completion — this is integrity at the exact point a real
        // Delegation is created, not a re-imposition of MeetingAction's ordinary (frontend-
        // owned) DoerId optionality during normal create/edit.
        var missing = actions.Where(a => string.IsNullOrWhiteSpace(a.DoerId)).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing.Select(a =>
                string.IsNullOrWhiteSpace(a.Title) ? $"action #{a.Id}" : $"\"{a.Title.Trim()}\""));
            throw new BusinessRuleException(
                $"Meeting cannot be completed because {(missing.Count == 1 ? "action" : "actions")} {names} " +
                $"{(missing.Count == 1 ? "has" : "have")} no assigned doer.");
        }

        // Meeting BusinessModule resolved dynamically (never hardcoded) — same
        // name-based convention already used by RequireLinkedWorkflowAsync above.
        var meetingModule = await _db.BusinessModules
            .Where(b => b.IsActive && !b.IsDeleted && b.Name.Trim().ToLower() == "meeting")
            .SingleAsync(ct);

        // Idempotency: a MeetingAction whose Delegation already exists (same
        // SourceBusinessModuleId + SourceEntityId = MeetingAction.Id) is skipped rather
        // than duplicated. Structurally, two concurrent completions of the SAME meeting
        // cannot both reach here — RequireLinkedWorkflowAsync's caller already takes a
        // Postgres row lock (SELECT ... FOR UPDATE) on this Meeting row before this point,
        // so the second request blocks until the first commits or rolls back, then sees
        // meeting.CompletedAt already set and fails the earlier "Meeting is completed..."
        // precondition before ever reaching MeetingAction/Delegation logic. This check
        // remains as defense in depth for any future retry path, not because the race is
        // currently reachable.
        var actionIds = actions.Select(a => a.Id.ToString(CultureInfo.InvariantCulture)).ToList();
        var alreadyLinked = await _db.Delegations.AsNoTracking()
            .Where(d => !d.IsDeleted && d.SourceBusinessModuleId == meetingModule.Id
                && d.SourceEntityId != null && actionIds.Contains(d.SourceEntityId))
            .Select(d => d.SourceEntityId!)
            .ToListAsync(ct);
        var alreadyLinkedSet = alreadyLinked.ToHashSet();

        foreach (var action in actions)
        {
            var sourceEntityId = action.Id.ToString(CultureInfo.InvariantCulture);
            if (alreadyLinkedSet.Contains(sourceEntityId)) continue;

            var command = new DelegationCreateCommand
            {
                Title = action.Title,
                Description = action.Description,
                DoerId = action.DoerId,
                DoerNameSnapshot = action.DoerName,
                Priority = action.Priority,
                DueDate = action.DueDate,
                SourceBusinessModuleId = meetingModule.Id,
                SourceEntityId = sourceEntityId,
                SourceReference = meeting.MeetingNumber
            };
            await _delegations.CreateCoreAsync(command, ct);
        }
    }
}
