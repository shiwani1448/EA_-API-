using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.Ai;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class MeetingDelegationService(
    EaFmsDbContext db, DelegationService delegations, IAuditService audit)
{
    // Both decision paths serialize on the same row, including retries with existing action IDs.
    private async Task<Meeting> LockMeetingAsync(long id, CancellationToken ct)
    {
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM public.ea_meetings WHERE \"Id\" = {id} FOR UPDATE", ct);
        var meeting = await db.Meetings.SingleOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"Meeting {id} not found.");
        await db.Entry(meeting).ReloadAsync(ct);
        if (meeting.IsDeleted) throw new NotFoundException($"Meeting {id} not found.");
        return meeting;
    }

    public async Task DeclineAsync(long id, string actor, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var meeting = await LockMeetingAsync(id, ct);
            if (!meeting.CompletedAt.HasValue)
                throw new BusinessRuleException("Complete the meeting before choosing whether to delegate tasks.");
            if (meeting.DelegationDecision is not null)
                throw new BusinessRuleException("Delegation has already been decided for this meeting.");
            Decide(meeting, "Declined", actor);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<MeetingAiActionsConfirmResponseDto> ConfirmAsync(
        long id, ConfirmMeetingAiActionsRequestDto request, string actor, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var meeting = await LockMeetingAsync(id, ct);
            if (!meeting.CompletedAt.HasValue)
                throw new BusinessRuleException("Complete the meeting before delegating tasks.");
            if (meeting.DelegationDecision == "Declined")
                throw new BusinessRuleException("Delegation was declined for this meeting.");
            if (request.Actions is null || request.Actions.Count == 0)
                throw new BusinessRuleException("At least one action is required.");
            if (request.Actions.Any(a => a is null || string.IsNullOrWhiteSpace(a.Title)
                || string.IsNullOrWhiteSpace(a.DoerId) || string.IsNullOrWhiteSpace(a.DoerName)))
                throw new BusinessRuleException("Each action requires title, doerId and doerName.");
            // A delegationType must resolve to exactly one Delegation TAT rule, otherwise the
            // Delegation created below would fail; reject it here with a readable 409.
            foreach (var item in request.Actions)
                await MeetingActionFactory.ValidateAsync(db, item, ct);
            var ids = request.Actions.Where(a => a.MeetingActionId.HasValue).Select(a => a.MeetingActionId!.Value).ToList();
            if (ids.Distinct().Count() != ids.Count)
                throw new BusinessRuleException("An action may only be submitted once per request.");
            var existing = await db.MeetingActions.Where(a => ids.Contains(a.Id) && a.MeetingId == id && !a.IsDeleted)
                .ToDictionaryAsync(a => a.Id, ct);
            if (existing.Count != ids.Count)
                throw new BusinessRuleException("Action does not belong to this meeting or is deleted.");
            var links = await LoadDelegationIdsAsync(db, ids, ct);
            foreach (var action in existing.Values)
                if (links.ContainsKey(action.Id))
                    throw new BusinessRuleException($"Action '{action.Title}' is already delegated.");

            var now = Clock.UtcNowTz;
            var saved = new List<MeetingAction>();
            foreach (var item in request.Actions)
            {
                var values = MeetingActionFactory.Build(id, item, actor, now);
                if (item.MeetingActionId is long actionId)
                {
                    var action = existing[actionId];
                    action.Title = values.Title;
                    action.Description = values.Description;
                    action.DoerId = values.DoerId;
                    action.DoerName = values.DoerName;
                    action.Priority = values.Priority;
                    action.DueDate = values.DueDate;
                    action.StartDate = values.StartDate;
                    action.AssigneeId = values.AssigneeId;
                    action.AssigneeName = values.AssigneeName;
                    action.DelegationType = values.DelegationType;
                    action.ModifiedBy = actor;
                    action.ModifiedDate = now;
                    saved.Add(action);
                }
                else
                {
                    db.MeetingActions.Add(values);
                    saved.Add(values);
                }
            }
            await db.SaveChangesAsync(ct);
            var response = new MeetingAiActionsConfirmResponseDto
            {
                MeetingId = id, CreatedDelegationCount = saved.Count, DelegationDecision = "Delegated"
            };
            foreach (var action in saved)
            {
                var delegation = await CreateForActionAsync(meeting, action, ct);
                var dto = MeetingActionFactory.ToDto(action, now);
                dto.DelegationId = delegation.DelegationId;
                response.CreatedActions.Add(dto);
            }
            if (meeting.DelegationDecision is null) Decide(meeting, "Delegated", actor);
            await db.SaveChangesAsync(ct);
            await AiSuggestionWriters.MarkMeetingActionExtractionAppliedAsync(db, id, response, ct);
            await tx.CommitAsync(ct);
            return response;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private void Decide(Meeting meeting, string decision, string actor)
    {
        meeting.DelegationDecision = decision;
        meeting.DelegationDecidedAt = Clock.UtcNowTz;
        meeting.DelegationDecidedBy = actor;
        meeting.ModifiedBy = actor;
        meeting.ModifiedDate = meeting.DelegationDecidedAt;
        audit.AddAudit("MEETING_DELEGATION_DECISION", "Meeting", nameof(Meeting),
            meeting.Id.ToString(CultureInfo.InvariantCulture), null,
            new { meeting.DelegationDecision, meeting.DelegationDecidedAt, meeting.DelegationDecidedBy },
            $"Meeting delegation {decision.ToLowerInvariant()}");
    }

    // Caller owns the transaction and meeting row lock. CreateCoreAsync retains all existing
    // delegation/EaTask defaults, ownership, numbering and audit behavior.
    internal async Task<DelegationResponseDto> CreateForActionAsync(Meeting meeting, MeetingAction action, CancellationToken ct)
    {
        var moduleId = await db.BusinessModules.Where(b => b.IsActive && !b.IsDeleted && b.Name.Trim().ToLower() == "meeting")
            .Select(b => b.Id).SingleAsync(ct);
        return await delegations.CreateCoreAsync(new DelegationCreateCommand
        {
            Title = action.Title, Description = action.Description,
            DoerId = action.DoerId, DoerNameSnapshot = action.DoerName,
            AssigneeId = action.AssigneeId, AssigneeNameSnapshot = action.AssigneeName,
            DelegationType = action.DelegationType, StartDate = action.StartDate,
            Priority = action.Priority, DueDate = action.DueDate,
            SourceBusinessModuleId = moduleId,
            SourceEntityId = action.Id.ToString(CultureInfo.InvariantCulture),
            SourceReference = meeting.MeetingNumber
        }, ct);
    }

    internal static async Task<Dictionary<long, long>> LoadDelegationIdsAsync(
        EaFmsDbContext db, IEnumerable<long> actionIds, CancellationToken ct)
    {
        var ids = actionIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
        // Include deleted delegations: an action that has ever been delegated cannot be delegated twice.
        var links = await db.Delegations.AsNoTracking().Where(d => d.SourceEntityId != null
            && ids.Contains(d.SourceEntityId) && db.BusinessModules.Any(b => b.Id == d.SourceBusinessModuleId
                && b.Name.Trim().ToLower() == "meeting"))
            .Select(d => new { d.Id, d.SourceEntityId }).ToListAsync(ct);
        return links.GroupBy(d => long.Parse(d.SourceEntityId!, CultureInfo.InvariantCulture))
            .ToDictionary(g => g.Key, g => g.Min(d => d.Id));
    }
}

