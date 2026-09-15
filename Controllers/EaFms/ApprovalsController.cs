using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[ApiController]
[Route("api/ea/approvals")]
public class ApprovalsController : ControllerBase
{
    private readonly ApprovalService _service;
    // No-op refresh for DI changes.
    public ApprovalsController(ApprovalService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ApprovalRequestDto dto, CancellationToken cancellationToken)
    {
        var entity = new ApprovalRequest
        {
            RequestTitle = dto.RequestTitle,
            RequestType = dto.RequestType,
            RequestedBy = dto.RequestedBy,
            Department = dto.Department,
            Priority = dto.Priority,
            Description = dto.Description,
            Justification = dto.Justification,
            Amount = dto.Amount,
            Currency = dto.Currency,
            RequiredApprovalDate = dto.RequiredApprovalDate,
            ApproverId = dto.ApproverId,
            ApproverName = dto.ApproverName,
            CreatedBy = dto.CreatedBy
        };

        var created = await _service.CreateDraftAsync(entity, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, new
        {
            approvalRequestId = created.Id,
            eaTaskId = created.EaTaskId,
            referenceNo = created.ReferenceNo,
            status = created.WorkflowStatus
        });
    }

    [HttpGet("{id}")]
    public IActionResult Get(long id) => Ok();

    [HttpPut("{id}/draft")]
    public async Task<IActionResult> SaveDraft(long id, [FromBody] ApprovalRequestDto dto, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateDraftAsync(id, existing =>
        {
            existing.RequestTitle = dto.RequestTitle;
            existing.RequestType = dto.RequestType;
            existing.RequestedBy = dto.RequestedBy;
            existing.Department = dto.Department;
            existing.Priority = dto.Priority;
            existing.Description = dto.Description;
            existing.Justification = dto.Justification;
            existing.Amount = dto.Amount;
            existing.Currency = dto.Currency;
            existing.RequiredApprovalDate = dto.RequiredApprovalDate;
            existing.ApproverId = dto.ApproverId;
            existing.ApproverName = dto.ApproverName;
        }, cancellationToken);

        return Ok(new
        {
            approvalRequestId = updated.Id,
            eaTaskId = updated.EaTaskId,
            referenceNo = updated.ReferenceNo,
            status = updated.WorkflowStatus
        });
    }
}
