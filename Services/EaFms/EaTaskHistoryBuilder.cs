using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Builds an EaTask's central execution timeline (Created/Started/Paused/Resumed/
/// Completed/Cancelled) purely by reading existing authoritative sources — never a new
/// history table. For Meeting (the only module with a WorkflowInstance today) this reads
/// WorkflowHistory + WorkPause; for the no-TAT modules it reads each module's own existing
/// audit trail. Nothing here is fabricated: an event with no reliable source is omitted
/// rather than guessed, and actor/previous-status fields are null when not provable.
/// </summary>
internal sealed class EaTaskHistoryBuilder(EaFmsDbContext db)
{
    public async Task<List<EaTaskHistoryEventDto>> BuildAsync(long eaTaskId, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == eaTaskId, ct)
            ?? throw new NotFoundException($"EA task {eaTaskId} not found.");

        var events = new List<EaTaskHistoryEventDto>
        {
            new()
            {
                EventType = "Created",
                PreviousExecutionStatus = null,
                NewExecutionStatus = EaTaskExecutionStatus.NotStarted,
                OccurredAt = task.CreatedDate,
                PerformedBy = task.CreatedBy,
                AllottedTatMinutes = task.AllottedTatMinutes,
                Source = "EaTask"
            }
        };

        // A Delegation's WorkflowInstanceId is only its pause anchor (created on first Pause) — its
        // Started/Completed events still come from the Delegation audit trail, pauses from WorkPause.
        // Same for a Follow-up's own task: its WorkflowInstanceId is only a pause anchor too.
        if (task.WorkflowInstanceId.HasValue
            && !string.Equals(task.ModuleName, "Delegation", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(task.ModuleName, "Follow-up", StringComparison.OrdinalIgnoreCase))
            await AddMeetingEventsAsync(task, events, ct);
        else
            await AddNoTatModuleEventsAsync(task, events, ct);

        return events.OrderBy(e => e.OccurredAt).ToList();
    }

    private async Task AddMeetingEventsAsync(EaTask task, List<EaTaskHistoryEventDto> events, CancellationToken ct)
    {
        var workflowId = task.WorkflowInstanceId!.Value;

        // Real status-change rows only (same-status pause/resume bookkeeping rows carry
        // FromStatusId == ToStatusId and a TransitionType — excluded here by that check).
        var transitions = await db.WorkflowHistory.AsNoTracking()
            .Where(h => h.WorkflowInstanceId == workflowId && h.FromStatusId != h.ToStatusId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(ct);

        var pauses = await db.WorkPauses.AsNoTracking()
            .Where(p => p.WorkflowInstanceId == workflowId && !p.IsDeleted)
            .OrderBy(p => p.StartAt)
            .ToListAsync(ct);

        if (task.StartedAt.HasValue)
        {
            // WorkflowInstance.TatStartedAt and this history row's ChangedAt are written
            // from the same `now` inside WorkflowService.TransitionAsync — exact match.
            var started = transitions.FirstOrDefault(h => h.ChangedAt == task.StartedAt.Value);
            events.Add(new EaTaskHistoryEventDto
            {
                EventType = "Started",
                PreviousExecutionStatus = EaTaskExecutionStatus.NotStarted,
                NewExecutionStatus = EaTaskExecutionStatus.InProgress,
                OccurredAt = task.StartedAt.Value,
                PerformedBy = started?.CreatedBy,
                AllottedTatMinutes = task.AllottedTatMinutes,
                CurrentTatUsedMinutes = task.AllottedTatMinutes.HasValue ? 0 : null,
                CurrentTatDifferenceMinutes = task.AllottedTatMinutes,
                Source = started is not null ? "WorkflowHistory" : "EaTask"
            });

            AddPauseEvents(task, pauses, events);
        }

        if (task.CompletedAt.HasValue)
        {
            var completed = transitions.FirstOrDefault(h => h.ChangedAt == task.CompletedAt.Value);
            events.Add(new EaTaskHistoryEventDto
            {
                EventType = "Completed",
                PreviousExecutionStatus = EaTaskExecutionStatus.InProgress,
                NewExecutionStatus = EaTaskExecutionStatus.Completed,
                OccurredAt = task.CompletedAt.Value,
                PerformedBy = completed?.CreatedBy,
                AllottedTatMinutes = task.AllottedTatMinutes,
                CurrentTatUsedMinutes = task.TatUsedMinutes,
                CurrentTatDifferenceMinutes = task.AllottedTatMinutes - task.TatUsedMinutes,
                Source = completed is not null ? "WorkflowHistory" : "EaTask"
            });
        }
    }

    /// <summary>Paused/Resumed events from shared WorkPause rows (used by Meeting and by Delegation pause anchors).</summary>
    private static void AddPauseEvents(EaTask task, IReadOnlyList<WorkPause> pauses, List<EaTaskHistoryEventDto> events)
    {
        if (task.StartedAt is not { } startedAt) return;

        foreach (var pause in pauses.Where(p => p.StartAt >= startedAt))
        {
            var priorPauses = pauses.Where(p => p.StartAt < pause.StartAt).ToList();
            var usedAtPauseStart = ElapsedMinusPaused(startedAt, pause.StartAt, priorPauses);
            events.Add(new EaTaskHistoryEventDto
            {
                EventType = "Paused",
                PreviousExecutionStatus = EaTaskExecutionStatus.InProgress,
                NewExecutionStatus = EaTaskExecutionStatus.InProgress,
                OccurredAt = pause.StartAt,
                PerformedBy = pause.CreatedBy,
                IsPaused = true,
                AllottedTatMinutes = task.AllottedTatMinutes,
                CurrentTatUsedMinutes = usedAtPauseStart,
                CurrentTatDifferenceMinutes = task.AllottedTatMinutes - usedAtPauseStart,
                PauseStartedAt = pause.StartAt,
                Notes = pause.Reason,
                Source = "WorkPause"
            });

            if (pause.EndAt.HasValue)
            {
                var pausesUpToAndIncluding = pauses.Where(p => p.StartAt <= pause.StartAt).ToList();
                var usedAtResume = ElapsedMinusPaused(startedAt, pause.EndAt.Value, pausesUpToAndIncluding);
                events.Add(new EaTaskHistoryEventDto
                {
                    EventType = "Resumed",
                    PreviousExecutionStatus = EaTaskExecutionStatus.InProgress,
                    NewExecutionStatus = EaTaskExecutionStatus.InProgress,
                    OccurredAt = pause.EndAt.Value,
                    PerformedBy = pause.ResumedById ?? pause.CreatedBy,
                    PerformedByName = pause.ResumedByName,
                    IsPaused = false,
                    AllottedTatMinutes = task.AllottedTatMinutes,
                    CurrentTatUsedMinutes = usedAtResume,
                    CurrentTatDifferenceMinutes = task.AllottedTatMinutes - usedAtResume,
                    PauseStartedAt = pause.StartAt,
                    PauseEndedAt = pause.EndAt,
                    PauseDurationMinutes = (int)(pause.EndAt.Value - pause.StartAt).TotalMinutes,
                    Notes = pause.ResumedReason,
                    Source = "WorkPause"
                });
            }
        }
    }

    private static int? ElapsedMinusPaused(DateTime start, DateTime at, IReadOnlyCollection<WorkPause> pausesSoFar)
    {
        var paused = WorkPauseClassifier.GetPausedDuration(start, at, pausesSoFar);
        var used = at - start - paused;
        if (used < TimeSpan.Zero) used = TimeSpan.Zero;
        return (int)used.TotalMinutes;
    }

    private async Task AddNoTatModuleEventsAsync(EaTask task, List<EaTaskHistoryEventDto> events, CancellationToken ct)
    {
        if (string.Equals(task.ModuleName, "Travel & Hospitality", StringComparison.OrdinalIgnoreCase))
        {
            var logs = await db.AuditLogs.AsNoTracking()
                .Where(a => a.Module == "Travel" && a.EntityName == "TravelRequest" && a.EntityId == task.BusinessRecordId
                    && (a.ActionType == "TRAVEL_START" || a.ActionType == "TRAVEL_COMPLETE" || a.ActionType == "TRAVEL_CANCEL"))
                .OrderBy(a => a.OccurredAt)
                .ToListAsync(ct);
            foreach (var log in logs)
                events.Add(MapAuditEvent(log, log.ActionType switch
                {
                    "TRAVEL_START" => ("Started", EaTaskExecutionStatus.NotStarted, EaTaskExecutionStatus.InProgress),
                    "TRAVEL_COMPLETE" => ("Completed", EaTaskExecutionStatus.InProgress, EaTaskExecutionStatus.Completed),
                    "TRAVEL_CANCEL" => ("Cancelled", null, EaTaskExecutionStatus.Cancelled),
                    _ => (log.ActionType, null, null)
                }));
        }
        else if (string.Equals(task.ModuleName, "EA Approval", StringComparison.OrdinalIgnoreCase))
        {
            // Approval's own AuditLog uses the request's numeric Id, but EaTask.BusinessRecordId
            // is the ApprovalRequest.ReferenceNo (Approval never runs the two-phase Id
            // correction Travel/Delegation do) — resolve the numeric Id first.
            var approvalId = await db.ApprovalRequests.AsNoTracking()
                .Where(a => a.ReferenceNo == task.BusinessRecordId)
                .Select(a => (long?)a.Id)
                .FirstOrDefaultAsync(ct);
            if (approvalId.HasValue)
            {
                var entityId = approvalId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var logs = await db.AuditLogs.AsNoTracking()
                    .Where(a => a.Module == "Approval" && a.EntityName == "ApprovalRequest" && a.EntityId == entityId
                        && (a.ActionType == "APPROVAL_SUBMIT" || a.ActionType == "APPROVAL_APPROVE" || a.ActionType == "APPROVAL_REJECT"))
                    .OrderBy(a => a.OccurredAt)
                    .ToListAsync(ct);
                foreach (var log in logs)
                    events.Add(MapAuditEvent(log, log.ActionType switch
                    {
                        "APPROVAL_SUBMIT" => ("Started", EaTaskExecutionStatus.NotStarted, EaTaskExecutionStatus.InProgress),
                        "APPROVAL_APPROVE" or "APPROVAL_REJECT" => ("Completed", EaTaskExecutionStatus.InProgress, EaTaskExecutionStatus.Completed),
                        _ => (log.ActionType, null, null)
                    }));
            }
        }
        else if (string.Equals(task.ModuleName, "Delegation", StringComparison.OrdinalIgnoreCase))
        {
            // Delegation.EaTaskId's BusinessRecordId is already corrected to Delegation.Id
            // at creation (the same two-phase pattern Travel uses) — no ReferenceNo lookup needed.
            var logs = await db.AuditLogs.AsNoTracking()
                .Where(a => a.Module == "Delegation" && a.EntityName == "Delegation" && a.EntityId == task.BusinessRecordId
                    && (a.ActionType == "DELEGATION_START" || a.ActionType == "DELEGATION_COMPLETE"))
                .OrderBy(a => a.OccurredAt)
                .ToListAsync(ct);
            foreach (var log in logs)
                events.Add(MapAuditEvent(log, log.ActionType switch
                {
                    "DELEGATION_START" => ("Started", EaTaskExecutionStatus.NotStarted, EaTaskExecutionStatus.InProgress),
                    "DELEGATION_COMPLETE" => ("Completed", EaTaskExecutionStatus.InProgress, EaTaskExecutionStatus.Completed),
                    _ => (log.ActionType, null, null)
                }));

            if (task.WorkflowInstanceId.HasValue && task.StartedAt.HasValue)
            {
                var workflowId = task.WorkflowInstanceId.Value;
                var pauses = await db.WorkPauses.AsNoTracking()
                    .Where(p => p.WorkflowInstanceId == workflowId && !p.IsDeleted)
                    .OrderBy(p => p.StartAt)
                    .ToListAsync(ct);
                AddPauseEvents(task, pauses, events);
            }
        }
        else if (string.Equals(task.ModuleName, "Follow-up", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(task.BusinessRecordId, out var followupId))
        {
            // Started/Completed from the Followup audit trail (same as Delegation's branch).
            var logs = await db.AuditLogs.AsNoTracking()
                .Where(a => a.Module == "Followup" && a.EntityName == "Followup" && a.EntityId == task.BusinessRecordId
                    && (a.ActionType == "FOLLOWUP_START" || a.ActionType == "FOLLOWUP_COMPLETE"))
                .OrderBy(a => a.OccurredAt)
                .ToListAsync(ct);
            foreach (var log in logs)
                events.Add(MapAuditEvent(log, log.ActionType switch
                {
                    "FOLLOWUP_START" => ("Started", EaTaskExecutionStatus.NotStarted, EaTaskExecutionStatus.InProgress),
                    "FOLLOWUP_COMPLETE" => ("Completed", EaTaskExecutionStatus.InProgress, EaTaskExecutionStatus.Completed),
                    _ => (log.ActionType, null, null)
                }));

            // Completion note is on the Followup itself, not the audit summary.
            var completionNote = await db.Followups.AsNoTracking().Where(f => f.Id == followupId)
                .Select(f => f.CompletionNote).FirstOrDefaultAsync(ct);
            foreach (var e in events.Where(e => e.EventType == "Completed" && e.Source == "AuditLog"))
                e.Notes = completionNote;

            if (task.WorkflowInstanceId.HasValue && task.StartedAt.HasValue)
            {
                var workflowId = task.WorkflowInstanceId.Value;
                var pauses = await db.WorkPauses.AsNoTracking()
                    .Where(p => p.WorkflowInstanceId == workflowId && !p.IsDeleted)
                    .OrderBy(p => p.StartAt)
                    .ToListAsync(ct);
                AddPauseEvents(task, pauses, events);
            }

            var reminders = await db.FollowupReminderLogs.AsNoTracking()
                .Where(r => r.FollowupId == followupId).OrderBy(r => r.SentAt).ToListAsync(ct);
            foreach (var r in reminders)
                events.Add(new EaTaskHistoryEventDto
                {
                    EventType = "ReminderSent",
                    OccurredAt = r.SentAt,
                    PerformedBy = r.SentById,
                    PerformedByName = r.SentByName,
                    Notes = $"{r.Channel} to {r.Recipient}" + (string.IsNullOrWhiteSpace(r.RecipientName) ? "" : $" ({r.RecipientName})"),
                    Source = "FollowupReminderLog"
                });

            var cycles = await db.FollowupCycles.AsNoTracking()
                .Where(c => c.FollowupId == followupId).OrderBy(c => c.FollowedUpAt).ToListAsync(ct);
            foreach (var c in cycles)
                events.Add(new EaTaskHistoryEventDto
                {
                    EventType = "FollowupRecorded",
                    OccurredAt = c.FollowedUpAt,
                    PerformedBy = c.FollowedUpByEmployeeId ?? c.CreatedBy,
                    PerformedByName = c.FollowedUpByEmployeeName,
                    Notes = c.Note + (c.NextFollowupAt.HasValue
                        ? $" | Next follow-up: {c.NextFollowupAt.Value:yyyy-MM-dd HH:mm}" : string.Empty),
                    Source = "FollowupCycle"
                });
        }
        // No other module currently creates EaTasks.
    }

    private static EaTaskHistoryEventDto MapAuditEvent(AuditLog log, (string EventType, string? Previous, string? New) mapping) => new()
    {
        EventType = mapping.EventType,
        PreviousExecutionStatus = mapping.Previous,
        NewExecutionStatus = mapping.New,
        OccurredAt = log.OccurredAt,
        PerformedBy = log.ActorName ?? log.ActorId,
        Source = "AuditLog"
    };
}
