using Jarvis5.Data.EaFms;
using Jarvis5.Common;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis5.Services.EaFms;

public class ApprovalService
{
    private readonly EaFmsDbContext _context;
    private readonly IAuditService _audit;
    private readonly IApprovalNumberRepository _repo;
    private readonly IEaTaskService _eaTaskService;

    public ApprovalService(EaFmsDbContext context, IAuditService audit, IApprovalNumberRepository repo, IEaTaskService eaTaskService)
    {
        _context = context;
        _audit = audit;
        _repo = repo;
        _eaTaskService = eaTaskService;
    }

    public async Task<ApprovalRequest> CreateDraftAsync(ApprovalRequest request, CancellationToken ct = default)
    {
        // Validate business module exists
        var approvalModule = await _context.BusinessModules.FirstOrDefaultAsync(b => b.Name == "EA Approval" && b.IsActive && !b.IsDeleted, ct);
        if (approvalModule is null)
            throw new InvalidOperationException("EA Approval business module not registered in ea_business_modules.");

        using var tx = await _context.Database.BeginTransactionAsync(ct);

        // generate reference
        var reference = await _repo.GenerateNextReferenceNoAsync(ct);

        request.ReferenceNo = reference;
        request.WorkflowStatus = "Draft";
        request.CreatedAt = DateTime.UtcNow;
        request.CreatedBy = request.CreatedBy ?? "system";

        // Create the required central task before inserting ApprovalRequest. EaTaskId is a
        // non-nullable FK, so persisting the request with its default value would violate
        // the relationship. ReferenceNo is generated above and is the stable record key
        // available before the approval identity is assigned.
        var createTaskDto = new Jarvis5.Dtos.EaFms.CreateEaTaskDto
        {
            ModuleId = approvalModule.Id,
            BusinessRecordId = request.ReferenceNo,
            Task = request.RequestTitle ?? reference,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description?.Trim(),
            // For TAT resolution the shared service requires both Type and Subtype. Use RequestType as Type
            // and Department as Subtype. This follows the mapping convention for Approval where RequestType identifies
            // the approval classification and Department refines it for TAT lookup.
            Type = request.RequestType?.Trim(),
            Subtype = request.Department?.Trim(),
            WorkflowInstanceId = null
        };

        var eaTaskDto = await _eaTaskService.CreateAsync(createTaskDto, ct);

        // Persist the approval only after a valid central task exists.
        request.EaTaskId = eaTaskDto.EaTaskId;
        await _context.ApprovalRequests.AddAsync(request, ct);
        await _context.SaveChangesAsync(ct);

        // Audit
        _audit.AddAudit("Created", "EA.Approval", "ApprovalRequest", request.Id.ToString(), null, request, "Approval request created (Draft)");
        await _context.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        return request;
    }

    public async Task<ApprovalRequest> UpdateDraftAsync(long approvalRequestId, Action<ApprovalRequest> applyChanges, CancellationToken ct = default)
    {
        var existing = await _context.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalRequestId, ct);
        if (existing is null) throw new KeyNotFoundException("Approval request not found.");
        if (existing.IsDeleted) throw new NotFoundException("Approval request not found.");
        if (!string.Equals(existing.WorkflowStatus, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only Draft approvals may be edited via the draft endpoint.");

        var old = new ApprovalRequest
        {
            Id = existing.Id,
            EaTaskId = existing.EaTaskId,
            ReferenceNo = existing.ReferenceNo,
            RequestTitle = existing.RequestTitle,
            RequestType = existing.RequestType,
            RequestedBy = existing.RequestedBy,
            Department = existing.Department,
            Priority = existing.Priority,
            Description = existing.Description,
            Justification = existing.Justification,
            Amount = existing.Amount,
            Currency = existing.Currency,
            RequiredApprovalDate = existing.RequiredApprovalDate,
            ApproverId = existing.ApproverId,
            ApproverName = existing.ApproverName,
            WorkflowStatus = existing.WorkflowStatus
        };

        applyChanges(existing);
        // Protect immutable fields
        if (existing.ReferenceNo != old.ReferenceNo)
            throw new BusinessRuleException("ReferenceNo is immutable and cannot be changed.");
        if (existing.EaTaskId != old.EaTaskId)
            throw new BusinessRuleException("EaTaskId is immutable and cannot be changed.");
        if (existing.Id != old.Id)
            throw new BusinessRuleException("ApprovalRequest.Id cannot be changed.");

        existing.UpdatedAt = DateTime.UtcNow;

        _context.ApprovalRequests.Update(existing);
        await _context.SaveChangesAsync(ct);

        _audit.AddAudit("DraftSaved", "EA.Approval", "ApprovalRequest", existing.Id.ToString(), old, existing, "Draft saved");
        await _context.SaveChangesAsync(ct);

        return existing;
    }
}
