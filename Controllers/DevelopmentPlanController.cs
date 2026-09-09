using Jarvis5.Dtos.DevelopmentPlan;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/development-plan")]
public class DevelopmentPlanController : ControllerBase
{
    private readonly IDevelopmentPlanService _developmentPlanService;

    public DevelopmentPlanController(IDevelopmentPlanService developmentPlanService)
    {
        _developmentPlanService = developmentPlanService;
    }

    /// <summary>Creates one SCIH_Task row per module, with its selected stages
    /// (checklist loaded server-side from SCIH_StageMaster). Only usable once the
    /// request has reached Stage 5 (Development).</summary>
    [HttpPost("{requestId:long}")]
    [ProducesResponseType(typeof(List<TaskModuleDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TaskModuleDetailDto>>> Create(long requestId, [FromBody] CreateDevelopmentPlanDto dto, CancellationToken ct)
    {
        var result = await _developmentPlanService.CreateAsync(requestId, dto, ct);
        return Ok(result);
    }

    /// <summary>Returns the read-only request/analysis/solution-design/approval
    /// context plus all modules for a request. Task history is not included here —
    /// see GET /{requestId}/history.</summary>
    [HttpGet("{requestId:long}")]
    [ProducesResponseType(typeof(DevelopmentPlanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DevelopmentPlanDto>> Get(long requestId, CancellationToken ct)
    {
        var result = await _developmentPlanService.GetByRequestIdAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>Returns a single module by its task id.</summary>
    [HttpGet("task/{taskId:long}")]
    [ProducesResponseType(typeof(TaskModuleDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskModuleDetailDto>> GetModule(long taskId, CancellationToken ct)
    {
        var result = await _developmentPlanService.GetModuleByIdAsync(taskId, ct);
        return Ok(result);
    }

    /// <summary>Returns the full task history (module/stage created/updated/removed
    /// events) for a request.</summary>
    [HttpGet("{requestId:long}/history")]
    [ProducesResponseType(typeof(List<TaskHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TaskHistoryDto>>> GetHistory(long requestId, CancellationToken ct)
    {
        var result = await _developmentPlanService.GetHistoryByRequestIdAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>Updates a module's own fields (name, overall dates, priority) and
    /// its full stage selection in one call.</summary>
    [HttpPatch("{taskId:long}")]
    [ProducesResponseType(typeof(TaskModuleDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskModuleDetailDto>> UpdateModule(long taskId, [FromBody] UpdateTaskModuleDto dto, CancellationToken ct)
    {
        var result = await _developmentPlanService.UpdateModuleAsync(taskId, dto, ct);
        return Ok(result);
    }

    /// <summary>Patches a single stage inside a module's StageDetails — sibling
    /// stages are left untouched.</summary>
    [HttpPut("{taskId:long}/stage/{stageId:long}")]
    [ProducesResponseType(typeof(TaskModuleDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskModuleDetailDto>> UpdateStage(long taskId, long stageId, [FromBody] UpdateStageDto dto, CancellationToken ct)
    {
        var result = await _developmentPlanService.UpdateStageAsync(taskId, stageId, dto, ct);
        return Ok(result);
    }

    /// <summary>Soft deletes a module (IsDeleted = true) — the row is kept for audit history.</summary>
    [HttpDelete("{taskId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteModule(long taskId, CancellationToken ct)
    {
        await _developmentPlanService.DeleteModuleAsync(taskId, ct);
        return NoContent();
    }
}
