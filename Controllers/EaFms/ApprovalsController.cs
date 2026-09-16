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
    private readonly ApprovalQueryService _queries;
    // No-op refresh for DI changes.
    public ApprovalsController(ApprovalService service, ApprovalQueryService queries)
    {
        _service = service;
        _queries = queries;
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

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? priority, [FromQuery] string? approver, [FromQuery] string? requestedBy, [FromQuery] string? department, [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] DateTime? requiredFrom, [FromQuery] DateTime? requiredTo, [FromQuery] string? dueState, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await _queries.ListAsync(search, status, priority, approver, requestedBy, department, createdFrom, createdTo, requiredFrom, requiredTo, dueState, page, pageSize, ct);
        return Ok(result.Items);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Ok(await _queries.DashboardAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) { var result = await _queries.DetailAsync(id, ct); return result is null ? NotFound() : Ok(result); }

    [HttpGet("{id:long}/cycles")]
    public async Task<IActionResult> Cycles(long id, CancellationToken ct) { var result = await _queries.CyclesAsync(id, ct); return result is null ? NotFound() : Ok(result); }

    [HttpGet("{id:long}/history")]
    public async Task<IActionResult> History(long id, CancellationToken ct) { var result = await _queries.HistoryForAsync(id, ct); return result is null ? NotFound() : Ok(result); }

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
