using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

/// <summary>
/// EA FMS AI usage: every time an EA used an AI feature — who, where (module / feature / endpoint),
/// for what (record + task), the prompt, the full response and the tokens used. Recorded on every
/// call whether or not the EA accepted the result.
/// </summary>
[ApiController]
[Route("api/ea/ai-usage")]
public class EaAiUsageController(IEaAiUsageService service, IEaAiUsageLogger usage) : ControllerBase
{
    /// <summary>Paged list of AI calls, newest first. Filters: module, feature, status, businessRecordId, eaTaskId,
    /// employeeId/employeeName (the EA), from/to (UTC).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(Jarvis5.Common.PagedResult<EaAiUsageRowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] EaAiUsageQueryDto query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    /// <summary>One AI call in full: prompt, complete response, tokens and outcome.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(EaAiUsageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        Ok(await service.GetAsync(id, ct));

    /// <summary>The screen reports that the EA used this AI response (for view-only features such as an itinerary draft
    /// she copied). The id comes from the X-AI-Usage-Id header of the AI response. Apply / confirm / send actions are
    /// marked automatically and do not need this.</summary>
    [HttpPost("{id:long}/used")]
    [ProducesResponseType(typeof(EaAiUsageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkUsed(long id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] MarkAiUsageUsedRequestDto? body, CancellationToken ct)
    {
        if (!await usage.MarkUsedByIdAsync(id, body?.UsedValue, ct))
            throw new Jarvis5.Common.NotFoundException($"AI usage log {id} not found.");
        return Ok(await service.GetAsync(id, ct));
    }

    /// <summary>Call and token totals — overall, by module, by feature, by EA, by task and by day — for the same filters.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(EaAiUsageSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] EaAiUsageQueryDto query, CancellationToken ct) =>
        Ok(await service.SummaryAsync(query, ct));

    /// <summary>Everything AI did for one task (ea_tasks.Id): token totals, per feature, and every call.</summary>
    [HttpGet("tasks/{eaTaskId:long}")]
    [ProducesResponseType(typeof(EaAiTaskUsageDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForTask(long eaTaskId, CancellationToken ct) =>
        Ok(await service.ForTaskAsync(eaTaskId, ct));
}
