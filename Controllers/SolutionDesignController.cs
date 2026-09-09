using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/request/{requestId:long}")]
public class SolutionDesignController : ControllerBase
{
    private readonly ISolutionDesignService _solutionDesignService;

    public SolutionDesignController(ISolutionDesignService solutionDesignService)
    {
        _solutionDesignService = solutionDesignService;
    }

    /// <summary>Generates an AI solution design from the request's approved analysis
    /// and stores it as a draft (creating or overwriting the saved design row) so it
    /// can be reloaded and edited later via GET/PUT solution-design. Does not advance
    /// the request stage — only Approve does that.</summary>
    [HttpPost("generate-solution-design")]
    [ProducesResponseType(typeof(SolutionDesignDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SolutionDesignDetailDto>> GenerateSolutionDesign(long requestId, CancellationToken ct)
    {
        var result = await _solutionDesignService.GenerateAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>Fetches the latest saved solution design draft for a request, so it
    /// can be reloaded into the review/edit screen.</summary>
    [HttpGet("solution-design")]
    [ProducesResponseType(typeof(SolutionDesignDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SolutionDesignDetailDto>> GetSolutionDesign(long requestId, CancellationToken ct)
    {
        var result = await _solutionDesignService.GetByRequestIdAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>Saves the user-reviewed/edited solution design in place.</summary>
    [HttpPut("solution-design")]
    [ProducesResponseType(typeof(SolutionDesignDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SolutionDesignDetailDto>> UpdateSolutionDesign(long requestId, [FromBody] UpdateSolutionDesignDto dto, CancellationToken ct)
    {
        var result = await _solutionDesignService.UpdateAsync(requestId, dto, ct);
        return Ok(result);
    }

    /// <summary>Approves the solution design (locks it) and moves the request to
    /// Stage 3 (Solution Design).</summary>
    [HttpPost("solution-design/approve")]
    [ProducesResponseType(typeof(SolutionDesignDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SolutionDesignDetailDto>> ApproveSolutionDesign(long requestId, [FromBody] ApproveSolutionDesignDto? dto, CancellationToken ct)
    {
        var result = await _solutionDesignService.ApproveAsync(requestId, dto, ct);
        return Ok(result);
    }
}
