using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/followups")]
public class FollowupsController : ControllerBase
{
    private readonly IFollowupService _followupService;
    private readonly IEscalationService _escalationService;

    public FollowupsController(IFollowupService followupService, IEscalationService escalationService)
    {
        _followupService = followupService;
        _escalationService = escalationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] FollowupListQueryDto query, CancellationToken ct)
        => Ok(await _followupService.GetPagedAsync(query, ct));
    [HttpGet("summary")]
    [ProducesResponseType(typeof(FollowupSummaryResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary([FromQuery] FollowupListQueryDto query, CancellationToken ct)
        => Ok(await _followupService.GetSummaryAsync(query, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFollowupRequestDto? dto, CancellationToken ct)
    {
        dto ??= new CreateFollowupRequestDto();
        var created = await _followupService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// CANONICAL action for "EA performed another follow-up". In one transaction it updates the Followup's
    /// current snapshot (note, next/expected dates, last follow-up time, modifier) and appends exactly one
    /// history row (ea_followup_cycles) with who followed up, when, remark, next dates and outcome.
    /// Send employeeId/employeeName (the operator). Do not also call POST /cycles for the same event.
    /// </summary>
    [HttpPost("{id:long}/record-followup")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointSummary("Record a follow-up (canonical action: snapshot + one history row)")]
    [EndpointDescription("Use this when EA performed another follow-up. Updates the Followup's current snapshot and appends exactly one ea_followup_cycles history row (employeeId/employeeName = operator). Do not also call POST /cycles for the same event.")]
    public async Task<IActionResult> RecordFollowup(long id, [FromBody] RecordFollowupRequestDto? dto, CancellationToken ct)
    {
        dto ??= new RecordFollowupRequestDto();
        await _followupService.RecordFollowupAsync(id, dto, ct);
        return NoContent();
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var item = await _followupService.GetByIdAsync(id, ct);
        return Ok(item);
    }

    [HttpGet("/api/ea/intake/{intakeId:long}/followups")]
    public async Task<IActionResult> GetByIntake(long intakeId, CancellationToken ct)
    {
        var list = await _followupService.GetByIntakeRequestIdAsync(intakeId, ct);
        return Ok(list);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateFollowupRequestDto? dto, CancellationToken ct)
    {
        dto ??= new UpdateFollowupRequestDto();
        var updated = await _followupService.UpdateAsync(id, dto, ct);
        return Ok(updated);
    }

    /// <summary>Sends an explicit immediate reminder email using the persisted frontend-supplied recipient snapshot.</summary>
    [HttpPost("{id:long}/send-email")]
    public async Task<IActionResult> SendEmail(long id, CancellationToken ct)
    {
        await _followupService.SendEmailAsync(id, ct);
        return NoContent();
    }
    /// <summary>Returns a manual WhatsApp handoff using the persisted frontend-supplied phone snapshot; it does not deliver or open WhatsApp.</summary>
    [HttpPost("{id:long}/send-whatsapp")]
    [ProducesResponseType(typeof(FollowupWhatsAppActionResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendWhatsApp(long id, CancellationToken ct)
        => Ok(await _followupService.SendWhatsAppAsync(id, ct));
    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteFollowupRequestDto? dto, CancellationToken ct)
    {
        dto ??= new CompleteFollowupRequestDto();
        return Ok(await _followupService.CompleteAsync(id, dto, ct));
    }

    // Escalation creation is owned by EscalationsController: POST /api/ea/escalations (body carries FollowupId).
    // Removed here: a duplicate POST /api/ea/escalations (Swagger route conflict) and a second, followup-scoped create route.
    [HttpGet("{followupId:long}/escalations")]
    public async Task<IActionResult> GetEscalationsForFollowup(long followupId, CancellationToken ct)
    {
        var list = await _escalationService.GetByFollowupIdAsync(followupId, ct);
        return Ok(list);
    }
}
