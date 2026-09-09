using System.Globalization;
using Jarvis5.Common;
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

    public MeetingLifecycleService(
        EaFmsDbContext db,
        IWorkflowExecutionService execution,
        ICurrentUserService user,
        IAuditService audit)
    {
        _db = db;
        _execution = execution;
        _user = user;
        _audit = audit;
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
            ExecutionState = string.Equals(wf.StatusName, "Completed", StringComparison.OrdinalIgnoreCase) ? "Completed" : "Running",
            StartedAt = wf.TatStartedAt,
            TatSummary = new MeetingTatSummaryDto { Tat = null, TotalTat = TimeSpan.Zero, TatDifference = TimeSpan.Zero, StartTime = wf.TatStartedAt, EndTime = wf.CompletedAt, LastActiveTime = wf.CompletedAt ?? wf.TatStartedAt, PauseTime = TimeSpan.Zero, PauseCount = 0 },
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
            ExecutionState = pause.EndAt.HasValue ? "Running" : "Paused",
            StartedAt = pause.Workflow.TatStartedAt,
            TatSummary = new MeetingTatSummaryDto { Tat = null, TotalTat = TimeSpan.Zero, TatDifference = TimeSpan.Zero, StartTime = pause.Workflow.TatStartedAt, EndTime = pause.Workflow.CompletedAt, LastActiveTime = pause.EndAt.HasValue ? pause.EndAt : pause.StartAt, PauseTime = pause.EndAt.HasValue ? pause.EndAt.Value - pause.StartAt : Clock.UtcNowTz - pause.StartAt, PauseCount = 1 },
            CompletedAt = pause.Workflow.CompletedAt,
            IsActive = pause.Workflow.IsActive
        };

    public async Task<MeetingLifecycleResponseDto> StartAsync(long meetingId, MeetingStartRequestDto dto, CancellationToken ct)
    {
        var (_, workflowId) = await RequireLinkedWorkflowAsync(meetingId, ct);
        var wf = await _execution.StartAsync(workflowId, new StartWorkRequestDto { Notes = dto.Notes }, ct);
        return MapLifecycle(meetingId, null, wf, dto.Notes);
    }

    public async Task<MeetingPauseResponseDto> PauseAsync(long meetingId, MeetingPauseRequestDto dto, CancellationToken ct)
    {
        var (_, workflowId) = await RequireLinkedWorkflowAsync(meetingId, ct);
        var pause = await _execution.PauseAsync(workflowId, new SimplePauseRequestDto { Remark = dto.Remark }, ct);
        return MapPause(meetingId, pause);
    }

    public async Task<MeetingPauseResponseDto> ResumeAsync(long meetingId, MeetingResumeRequestDto dto, CancellationToken ct)
    {
        var (_, workflowId) = await RequireLinkedWorkflowAsync(meetingId, ct);
        var pause = await _execution.ResumePauseAsync(workflowId, new SimpleResumeRequestDto { Remark = dto.Remark }, ct);
        return MapPause(meetingId, pause);
    }

    public async Task<MeetingLifecycleResponseDto> CompleteAsync(long meetingId, MeetingCompleteRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Notes))
            throw new BusinessRuleException("Completion notes are required.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var (meeting, workflowId) = await RequireLinkedWorkflowAsync(meetingId, ct);
            var now = Clock.UtcNowTz;
            if (!meeting.CompletedAt.HasValue)
                meeting.CompletedAt = now;
            meeting.ModifiedBy = Actor;
            meeting.ModifiedDate = now;
            _audit.AddAudit("MEETING_COMPLETE", "Meeting", nameof(Meeting), meeting.Id.ToString(), null,
                new { meeting.Id, meeting.CompletedAt, dto.Notes }, "Meeting business completion");
            await _db.SaveChangesAsync(ct);

            var completed = await _execution.CompleteAsync(workflowId, new CompleteWorkRequestDto
            {
                Notes = dto.Notes.Trim(),
                EvidenceAttachmentIds = new List<long>()
            }, ct);

            await tx.CommitAsync(ct);
            return MapLifecycle(meetingId, meeting, completed.Workflow, dto.Notes);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

}
