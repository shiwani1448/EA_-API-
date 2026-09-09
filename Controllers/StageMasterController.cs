using Jarvis5.Dtos.DevelopmentPlan;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/stage-master")]
public class StageMasterController : ControllerBase
{
    private readonly IStageMasterService _stageMasterService;

    public StageMasterController(IStageMasterService stageMasterService)
    {
        _stageMasterService = stageMasterService;
    }

    /// <summary>All active stages (with their default checklists), for the frontend
    /// to dynamically load into a module's stage-selection screen.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<StageMasterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StageMasterDto>>> GetAll(CancellationToken ct)
    {
        var result = await _stageMasterService.GetAllAsync(ct);
        return Ok(result);
    }
}
