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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFollowupRequestDto dto, CancellationToken ct)
    {
        var created = await _followupService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/record-followup")]
    public async Task<IActionResult> RecordFollowup(long id, [FromBody] RecordFollowupRequestDto dto, CancellationToken ct)
    {
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
    public async Task<IActionResult> Update(long id, [FromBody] UpdateFollowupRequestDto dto, CancellationToken ct)
    {
        var updated = await _followupService.UpdateAsync(id, dto, ct);
        return Ok(updated);
    }

    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteFollowupRequestDto dto, CancellationToken ct)
    {
        return Ok(await _followupService.CompleteAsync(id, dto, ct));
    }

    // Escalations
    [HttpPost("/api/ea/escalations")]
    public async Task<IActionResult> CreateEscalation([FromBody] CreateEscalationRequestDto dto, CancellationToken ct)
    {
        var created = await _escalationService.CreateAsync(dto, ct);
        return CreatedAtAction("GetById", "Escalations", new { id = created.Id }, created);
    }
    // Followup-scoped escalation creation: POST /api/ea/followups/{id}/escalations
    [HttpPost("{id:long}/escalations")]
    public async Task<IActionResult> CreateEscalationForFollowup(long id, [FromBody] CreateEscalationRequestDto dto, CancellationToken ct)
    {
        // Reconcile route id and dto.FollowupId: prefer route id; if dto provides FollowupId it must match
        if (dto.FollowupId != 0 && dto.FollowupId != id)
        {
            return BadRequest(new { error = "FollowupId in body does not match route id." });
        }

        dto.FollowupId = id;

        var created = await _escalationService.CreateAsync(dto, ct);
        return CreatedAtAction("GetById", "Escalations", new { id = created.Id }, created);
    }

    [HttpGet("{followupId:long}/escalations")]
    public async Task<IActionResult> GetEscalationsForFollowup(long followupId, CancellationToken ct)
    {
        var list = await _escalationService.GetByFollowupIdAsync(followupId, ct);
        return Ok(list);
    }
}
