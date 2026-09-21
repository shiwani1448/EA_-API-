using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public partial class TravelRequestService
{
    // All Travel writers acquire the parent first, then its current cycle.
    // PostgreSQL's default ReadCommitted transaction plus FOR UPDATE serializes
    // state validation, including first submission when no child row exists yet.
    private async Task<TravelRequest> LockTravelParentAsync(long id, CancellationToken ct)
    {
        TravelRequest? parent;
        if (_db.Database.IsRelational())
        {
            var rows = await _db.TravelRequests.FromSqlInterpolated(
                $"SELECT * FROM public.ea_travel_requests WHERE \"Id\" = {id} AND NOT \"IsDeleted\" FOR UPDATE")
                .ToListAsync(ct);
            parent = rows.SingleOrDefault();
            // A scoped caller may already have tracked this row before taking the lock.
            if (parent is not null) await _db.Entry(parent).ReloadAsync(ct);
        }
        else
        {
            // EF InMemory supports state-machine tests, not locking/rollback tests.
            parent = await _db.TravelRequests.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        }
        return parent ?? throw new NotFoundException($"Travel request {id} not found.");
    }

    private async Task ValidateUnsubmittedTravelAsync(TravelRequest parent, CancellationToken ct)
    {
        if (parent.BusinessState != "Draft" || parent.SubmittedAt.HasValue ||
            parent.CurrentCycleNo != 0 ||
            parent.ApprovalState != ResolveDraftApprovalState(parent.ApprovalRequired) ||
            parent.ApprovedAt.HasValue || parent.RejectedAt.HasValue || parent.CompletedAt.HasValue ||
            await _db.TravelRequestCycles.AnyAsync(x => x.TravelRequestId == parent.Id, ct))
            throw new BusinessRuleException("Travel request is not a consistent unsubmitted draft.");
    }

    private async Task<TravelRequestCycle> LockCurrentTravelCycleAsync(
        TravelRequest parent, int expectedCycleNo, string state, CancellationToken ct)
    {
        if (parent.BusinessState != "Draft" || !parent.ApprovalRequired ||
            parent.ApprovalState != state || !parent.SubmittedAt.HasValue ||
            parent.CurrentCycleNo <= 0 || parent.CurrentCycleNo != expectedCycleNo ||
            parent.ApprovedAt.HasValue || parent.RejectedAt.HasValue || parent.CompletedAt.HasValue)
            throw new BusinessRuleException("Travel approval state or expected cycle conflicts with the current request.");

        TravelRequestCycle? cycle;
        if (_db.Database.IsRelational())
        {
            var rows = await _db.TravelRequestCycles.FromSqlInterpolated(
                $"SELECT * FROM public.ea_travel_request_cycles WHERE \"TravelRequestId\" = {parent.Id} AND \"CycleNo\" = {parent.CurrentCycleNo} FOR UPDATE")
                .ToListAsync(ct);
            cycle = rows.SingleOrDefault();
            if (cycle is not null) await _db.Entry(cycle).ReloadAsync(ct);
        }
        else
            cycle = await _db.TravelRequestCycles.SingleOrDefaultAsync(
                x => x.TravelRequestId == parent.Id && x.CycleNo == parent.CurrentCycleNo, ct);

        if (cycle is null) throw new NotFoundException("The current Travel approval cycle was not found.");
        var history = _db.TravelRequestCycles.Where(x => x.TravelRequestId == parent.Id);
        if (cycle.DecisionState != state || !cycle.SubmittedAt.HasValue ||
            (state == "Pending" && cycle.DecisionAt.HasValue) ||
            (state == "ChangesRequested" && !cycle.DecisionAt.HasValue) ||
            !string.Equals(cycle.ApproverId, parent.ApproverId, StringComparison.Ordinal) ||
            await history.CountAsync(ct) != parent.CurrentCycleNo ||
            await history.AnyAsync(x => x.CycleNo > parent.CurrentCycleNo || x.CycleNo <= 0 ||
                (x.CycleNo < parent.CurrentCycleNo && x.DecisionState != "ChangesRequested"), ct))
            throw new BusinessRuleException("Travel parent and approval cycle history are inconsistent.");
        return cycle;
    }

    private string TravelActor() =>
        _currentUser.UserName ?? _currentUser.UserId.ToString(CultureInfo.InvariantCulture);

    private static void ValidateTravelAction(int cycleNo, string? text)
    {
        if (cycleNo <= 0) throw new BadRequestException("ExpectedCycleNo must be greater than zero.");
        if (text?.Length > 4000) throw new BadRequestException("Action text must not exceed 4000 characters.");
    }

    private TravelRequestCycle NewTravelCycle(TravelRequest parent, int number, DateTime now)
    {
        var cycle = new TravelRequestCycle
        {
            TravelRequestId = parent.Id, CycleNo = number,
            SubmittedBy = TravelActor(), SubmittedAt = now,
            ApproverId = parent.ApproverId,
            // TRAVEL APPROVER SNAPSHOT RESOLUTION DEFERRED: preserve opaque IDs.
            ApproverNameSnapshot = parent.ApproverNameSnapshot,
            DecisionState = "Pending", CreatedBy = TravelActor(), CreatedDate = now
        };
        _db.TravelRequestCycles.Add(cycle);
        return cycle;
    }

    private static object TravelStateSnapshot(TravelRequest parent) => new
    {
        TravelRequestId = parent.Id, parent.ReferenceNo, parent.EaTaskId,
        parent.BusinessState, parent.ApprovalState, parent.CurrentCycleNo,
        parent.SubmittedAt, parent.ApprovedAt, parent.RejectedAt, parent.ApprovedBy, parent.RejectedBy
    };

    private void RecordTravelAction(string action, TravelRequest parent,
        TravelRequestCycle? cycle, object before, DateTime now, string? changesMade = null)
    {
        parent.ModifiedBy = TravelActor();
        parent.ModifiedDate = now;
        _audit.AddAudit(action, "Travel", nameof(TravelRequest),
            parent.Id.ToString(CultureInfo.InvariantCulture), before,
            new { Request = TravelStateSnapshot(parent), CurrentCycle = ToCurrentTravelCycle(cycle), ChangesMade = changesMade });
    }

    public async Task<TravelActionResponseDto> SubmitAsync(long travelRequestId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var parent = await LockTravelParentAsync(travelRequestId, ct);
        await ValidateUnsubmittedTravelAsync(parent, ct);
        var before = TravelStateSnapshot(parent);
        var now = Clock.UtcNowTz;
        parent.SubmittedAt = now;
        TravelRequestCycle? cycle = null;
        if (parent.ApprovalRequired)
        {
            cycle = NewTravelCycle(parent, 1, now);
            parent.CurrentCycleNo = 1;
            parent.ApprovalState = "Pending";
        }
        else parent.BusinessState = "Upcoming";
        RecordTravelAction("TRAVEL_SUBMIT", parent, cycle, before, now);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToTravelActionResponse(parent, cycle);
    }

    public Task<TravelActionResponseDto> ApproveAsync(long id, ApproveTravelRequestDto dto, CancellationToken ct = default) =>
        DecideTravelAsync(id, dto.ExpectedCycleNo, dto.DecisionComment, "Approved", "TRAVEL_APPROVE", ct,
            EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName));

    public Task<TravelActionResponseDto> RejectAsync(long id, RejectTravelRequestDto dto, CancellationToken ct = default) =>
        DecideTravelAsync(id, dto.ExpectedCycleNo, dto.DecisionComment, "Rejected", "TRAVEL_REJECT", ct,
            EaActorSnapshot.From(dto.EmployeeId, dto.EmployeeName));

    public Task<TravelActionResponseDto> RequestChangesAsync(long id, RequestTravelChangesDto dto, CancellationToken ct = default) =>
        DecideTravelAsync(id, dto.ExpectedCycleNo, dto.ChangeReason, "ChangesRequested", "TRAVEL_REQUEST_CHANGES", ct);

    private async Task<TravelActionResponseDto> DecideTravelAsync(long id, int expectedCycleNo,
        string? text, string decision, string action, CancellationToken ct, EaActorSnapshot? actor = null)
    {
        ValidateTravelAction(expectedCycleNo, text);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var parent = await LockTravelParentAsync(id, ct);
        var cycle = await LockCurrentTravelCycleAsync(parent, expectedCycleNo, "Pending", ct);
        var before = new { Request = TravelStateSnapshot(parent), CurrentCycle = ToCurrentTravelCycle(cycle) };
        var now = Clock.UtcNowTz;
        cycle.DecisionState = decision;
        if (decision == "ChangesRequested") cycle.ChangeReason = text?.Trim();
        else cycle.DecisionComment = text?.Trim();
        cycle.DecisionBy = TravelActor();
        cycle.DecisionAt = now;
        cycle.ModifiedBy = TravelActor();
        cycle.ModifiedDate = now;
        parent.ApprovalState = decision;
        if (decision == "Approved")
        {
            parent.BusinessState = "Upcoming";
            parent.ApprovedAt = now;
            // Decision actor: the frontend-supplied operator. A decision cannot be both, so RejectedBy is cleared.
            parent.RejectedBy = null;
            if (actor?.DisplayName is { } approver) parent.ApprovedBy = approver;
        }
        else if (decision == "Rejected")
        {
            parent.RejectedAt = now;
            parent.ApprovedBy = null;
            if (actor?.DisplayName is { } rejecter) parent.RejectedBy = rejecter;
        }
        RecordTravelAction(action, parent, cycle, before, now);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToTravelActionResponse(parent, cycle);
    }

    public async Task<TravelActionResponseDto> ResubmitAsync(long id, ResubmitTravelRequestDto dto, CancellationToken ct = default)
    {
        ValidateTravelAction(dto.ExpectedCycleNo, dto.ChangesMade);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var parent = await LockTravelParentAsync(id, ct);
        var previous = await LockCurrentTravelCycleAsync(parent, dto.ExpectedCycleNo, "ChangesRequested", ct);
        if (parent.CurrentCycleNo == int.MaxValue)
            throw new BusinessRuleException("Travel approval cycle limit reached.");
        var before = new { Request = TravelStateSnapshot(parent), CurrentCycle = ToCurrentTravelCycle(previous) };
        var now = Clock.UtcNowTz;
        previous.ChangesMade = dto.ChangesMade?.Trim();
        previous.ModifiedBy = TravelActor();
        previous.ModifiedDate = now;
        var cycle = NewTravelCycle(parent, parent.CurrentCycleNo + 1, now);
        parent.CurrentCycleNo = cycle.CycleNo;
        parent.ApprovalState = "Pending";
        // Parent SubmittedAt deliberately remains the first submission timestamp.
        RecordTravelAction("TRAVEL_RESUBMIT", parent, cycle, before, now, previous.ChangesMade);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToTravelActionResponse(parent, cycle);
    }

    private static bool IsApprovalSatisfied(TravelRequest parent) =>
        parent.ApprovalRequired ? parent.ApprovalState == "Approved" : parent.ApprovalState == "NotRequired";

    public async Task<TravelActionResponseDto> StartAsync(long travelRequestId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var parent = await LockTravelParentAsync(travelRequestId, ct);
        if (parent.BusinessState != "Upcoming" || !IsApprovalSatisfied(parent))
            throw new BusinessRuleException("Travel request cannot be started from its current state.");
        var before = TravelStateSnapshot(parent);
        var now = Clock.UtcNowTz;
        parent.BusinessState = "Active";
        parent.StartedAt = now;
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == parent.EaTaskId, ct);
        eaTask.ExecutionStatus = EaTaskExecutionStatus.InProgress;
        eaTask.StartedAt = now;
        RecordTravelAction("TRAVEL_START", parent, null, before, now);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToTravelActionResponse(parent, null);
    }

    public async Task<TravelActionResponseDto> CompleteAsync(long travelRequestId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var parent = await LockTravelParentAsync(travelRequestId, ct);
        if (parent.BusinessState != "Active")
            throw new BusinessRuleException("Travel request cannot be completed from its current state.");
        var before = TravelStateSnapshot(parent);
        var now = Clock.UtcNowTz;
        parent.BusinessState = "Completed";
        // CompletedAt is set once and never overwritten, even though this transition can
        // structurally only be reached once (Active -> Completed is not re-enterable).
        parent.CompletedAt ??= now;
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == parent.EaTaskId, ct);
        eaTask.ExecutionStatus = EaTaskExecutionStatus.Completed;
        eaTask.CompletedAt ??= parent.CompletedAt;
        RecordTravelAction("TRAVEL_COMPLETE", parent, null, before, now);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToTravelActionResponse(parent, null);
    }

    public async Task<TravelActionResponseDto> CancelAsync(long travelRequestId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var parent = await LockTravelParentAsync(travelRequestId, ct);
        // Completed and Cancelled are terminal; every other business state may be cancelled
        // (Draft, Upcoming, Active). ApprovalState and all child records are left untouched.
        if (parent.BusinessState is "Completed" or "Cancelled")
            throw new BusinessRuleException("Travel request cannot be cancelled from its current state.");
        var before = TravelStateSnapshot(parent);
        var now = Clock.UtcNowTz;
        parent.BusinessState = "Cancelled";
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == parent.EaTaskId, ct);
        eaTask.ExecutionStatus = EaTaskExecutionStatus.Cancelled;
        // CompletedAt stays null for Cancelled — it means successful completion only.
        // The cancellation moment remains available via audit/history (TRAVEL_CANCEL).
        RecordTravelAction("TRAVEL_CANCEL", parent, null, before, now);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToTravelActionResponse(parent, null);
    }

    private static TravelCurrentCycleDto? ToCurrentTravelCycle(TravelRequestCycle? cycle) =>
        cycle is null ? null : new TravelCurrentCycleDto
        {
            CycleNo = cycle.CycleNo, DecisionState = cycle.DecisionState!,
            SubmittedBy = cycle.SubmittedBy, SubmittedAt = cycle.SubmittedAt,
            ApproverId = cycle.ApproverId, ApproverNameSnapshot = cycle.ApproverNameSnapshot,
            ChangeReason = cycle.ChangeReason, ChangesMade = cycle.ChangesMade,
            DecisionComment = cycle.DecisionComment, DecisionBy = cycle.DecisionBy, DecisionAt = cycle.DecisionAt
        };

    private static TravelActionResponseDto ToTravelActionResponse(TravelRequest parent, TravelRequestCycle? cycle) => new()
    {
        TravelRequestId = parent.Id, EaTaskId = parent.EaTaskId, ReferenceNo = parent.ReferenceNo,
        BusinessState = parent.BusinessState, ApprovalState = parent.ApprovalState,
        CurrentCycleNo = parent.CurrentCycleNo, SubmittedAt = parent.SubmittedAt,
        ApprovedAt = parent.ApprovedAt, RejectedAt = parent.RejectedAt,
        ApprovedBy = parent.ApprovedBy, RejectedBy = parent.RejectedBy,
        StartedAt = parent.StartedAt, CompletedAt = parent.CompletedAt,
        CurrentCycle = ToCurrentTravelCycle(cycle)
    };
}
