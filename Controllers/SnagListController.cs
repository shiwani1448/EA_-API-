using Jarvis5.Dtos.SnagList;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/snaglist")]
public class SnagListController : ControllerBase
{
    private readonly ISnagListService _snagListService;

    public SnagListController(ISnagListService snagListService)
    {
        _snagListService = snagListService;
    }

    /// <summary>Creates one SCIH_SnagList row, with its selected stages (checklist
    /// loaded server-side from SCIH_StageMaster). RequestId/TaskId are optional -
    /// a Snag List can be raised standalone with no project/request link.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SnagListDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SnagListDetailDto>> Create([FromBody] CreateSnagListDto dto, CancellationToken ct)
    {
        var result = await _snagListService.CreateAsync(dto, ct);
        return Ok(result);
    }

    /// <summary>Returns every Snag List that has this employee assigned as the doer
    /// on at least one selected stage.</summary>
    [HttpGet("employee/{employeeId}")]
    [ProducesResponseType(typeof(List<SnagListDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SnagListDetailDto>>> GetByEmployee(string employeeId, CancellationToken ct)
    {
        var result = await _snagListService.GetByDoerIdAsync(employeeId, ct);
        return Ok(result);
    }

    /// <summary>Returns the snag's own details (with its stage details inline) plus its full task history.</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(SnagListDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SnagListDto>> Get(long id, CancellationToken ct)
    {
        var result = await _snagListService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Updates the Snag List's own fields (description, priority, overall status).</summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(SnagListDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SnagListDetailDto>> Update(long id, [FromBody] UpdateSnagListDto dto, CancellationToken ct)
    {
        var result = await _snagListService.UpdateAsync(id, dto, ct);
        return Ok(result);
    }

    /// <summary>Patches the Snag List's own fields (description, priority, overall status). Only non-null fields are changed.</summary>
    [HttpPatch("{id:long}")]
    [ProducesResponseType(typeof(SnagListDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SnagListDetailDto>> Patch(long id, [FromBody] UpdateSnagListDto dto, CancellationToken ct)
    {
        var result = await _snagListService.UpdateAsync(id, dto, ct);
        return Ok(result);
    }

    /// <summary>Patches a single stage inside a Snag List's StageDetails - sibling stages are left untouched.</summary>
    [HttpPut("{id:long}/stage/{stageId:long}")]
    [ProducesResponseType(typeof(SnagListDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SnagListDetailDto>> UpdateStage(long id, long stageId, [FromBody] UpdateSnagStageDto dto, CancellationToken ct)
    {
        var result = await _snagListService.UpdateStageAsync(id, stageId, dto, ct);
        return Ok(result);
    }

    /// <summary>Closes the Snag List once every selected stage - including Final Feedback,
    /// Technical Documentation, AI Documentation Review and User Training Documentation - is Completed.</summary>
    [HttpPost("{id:long}/close")]
    [ProducesResponseType(typeof(SnagListDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SnagListDetailDto>> Close(long id, CancellationToken ct)
    {
        var result = await _snagListService.CloseAsync(id, ct);
        return Ok(result);
    }
}
