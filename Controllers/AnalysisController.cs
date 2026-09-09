using Jarvis5.Dtos.Analysis;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/request/{requestId:long}")]
public class AnalysisController : ControllerBase
{
    private readonly IAnalysisService _analysisService;

    public AnalysisController(IAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    /// <summary>Generates an AI analysis for the request and stores it as a draft
    /// (creating or overwriting the saved analysis row) so it can be reloaded and
    /// edited later via GET/POST analysis. Does not advance the request stage.</summary>
    [HttpPost("generate-analysis")]
    [ProducesResponseType(typeof(AnalysisDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisDetailDto>> GenerateAnalysis(long requestId, CancellationToken ct)
    {
        var result = await _analysisService.GenerateAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>Saves the user-reviewed/edited analysis. On first save this moves
    /// the request from Stage 1 (Request Raised) to Stage 2 (Analysis).</summary>
    [HttpPost("analysis")]
    [ProducesResponseType(typeof(AnalysisDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisDetailDto>> SaveAnalysis(long requestId, [FromBody] SaveAnalysisDto dto, CancellationToken ct)
    {
        var result = await _analysisService.SaveAsync(requestId, dto, ct);
        return Ok(result);
    }

    /// <summary>Fetches the previously saved analysis for a request, so it can be
    /// reloaded into the review/edit screen (e.g. re-opening the page later).</summary>
    [HttpGet("analysis")]
    [ProducesResponseType(typeof(AnalysisDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisDetailDto>> GetAnalysis(long requestId, CancellationToken ct)
    {
        var result = await _analysisService.GetByRequestIdAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>Only usable right after a Director rejection. Asks the AI to
    /// improve (not regenerate) the analysis using the Director's rejection
    /// comments, and stores the result as a brand new, immutable version.</summary>
    [HttpPost("analysis/rework")]
    [ProducesResponseType(typeof(AnalysisDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisDetailDto>> ReworkAnalysis(long requestId, CancellationToken ct)
    {
        var result = await _analysisService.ReworkAsync(requestId, ct);
        return Ok(result);
    }

    /// <summary>All analysis versions for a request, oldest first — "View
    /// Previous Versions" on the Approval screen.</summary>
    [HttpGet("analysis/versions")]
    [ProducesResponseType(typeof(List<AnalysisDetailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AnalysisDetailDto>>> GetAnalysisVersions(long requestId, CancellationToken ct)
    {
        var result = await _analysisService.GetVersionsAsync(requestId, ct);
        return Ok(result);
    }
}
