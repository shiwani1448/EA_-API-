using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jarvis5.Services.EaFms;

public class ApprovalLifecycleService : IApprovalLifecycleService
{
    private readonly EaFmsDbContext _db;
    private readonly IAuditService _audit;

    public ApprovalLifecycleService(EaFmsDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

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
        var req = await _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId && !a.IsDeleted, ct)
            ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
        if (!string.Equals(req.WorkflowStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Only Draft approvals may be submitted.");

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
        // starts InProgress and never reaches Draft, so this is currently unreachable
        // through it — kept correct in case a Draft-creating caller is ever wired up.
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == req.EaTaskId, ct);
        eaTask.ExecutionStatus = EaTaskExecutionStatus.InProgress;
        eaTask.StartedAt ??= now;

        _audit.AddAudit("APPROVAL_SUBMIT", "Approval", nameof(ApprovalRequest), req.Id.ToString(), null, new { req.ReferenceNo, req.Id }, "Approval submitted");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return req;
    }

    public async Task<ApprovalRequest> ApproveAsync(long approvalRequestId, ApprovalDecisionDto dto, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var req = await _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId && !a.IsDeleted, ct)
            ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
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

        _audit.AddAudit("APPROVAL_APPROVE", "Approval", nameof(ApprovalRequest), req.Id.ToString(), null, new { req.ReferenceNo, req.Id, cycle.CycleNo, req.ApprovedBy }, "Approval approved");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return req;
    }

    public async Task<ApprovalRequest> RejectAsync(long approvalRequestId, ApprovalDecisionDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Comment)) throw new BusinessRuleException("Reject requires a non-empty reason.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var req = await _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId && !a.IsDeleted, ct)
            ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
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

        _audit.AddAudit("APPROVAL_REJECT", "Approval", nameof(ApprovalRequest), req.Id.ToString(), null, new { req.ReferenceNo, req.Id, cycle.CycleNo, req.RejectedBy }, "Approval rejected");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return req;
    }

    public async Task<ApprovalRequest> RequestChangesAsync(long approvalRequestId, ApprovalDecisionDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Comment)) throw new BusinessRuleException("Request changes requires a non-empty reason.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var req = await _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId && !a.IsDeleted, ct)
            ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
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
        var req = await _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId && !a.IsDeleted, ct)
            ?? throw new NotFoundException($"Approval {approvalRequestId} not found.");
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
}
