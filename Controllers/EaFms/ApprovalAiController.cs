using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[ApiController]
[Route("api/ea/approvals/{approvalRequestId:long}/ai")]
public class ApprovalAiController : ControllerBase
{
    private readonly IApprovalAiService _approvalAi;

    public ApprovalAiController(IApprovalAiService approvalAi)
    {
        _approvalAi = approvalAi;
    }

    /// <summary>Preview only. Judges completeness from the request's own fields and its
    /// documents' file names only (no OCR/content access). Writes nothing.</summary>
    [HttpPost("readiness")]
    [ProducesResponseType(typeof(ApprovalAiReadinessResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalAiReadinessResponseDto>> CheckReadiness(long approvalRequestId, CancellationToken ct)
    {
        var result = await _approvalAi.CheckReadinessAsync(approvalRequestId, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Suggests an approver purely from historical Approved requests
    /// in the same department — there is no employee/role directory, so this can never name
    /// anyone who wasn't a real approver of a past request. Writes nothing.</summary>
    [HttpPost("recommend-approver")]
    [ProducesResponseType(typeof(ApprovalAiApproverSuggestionResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalAiApproverSuggestionResponseDto>> RecommendApprover(long approvalRequestId, CancellationToken ct)
    {
        var result = await _approvalAi.RecommendApproverAsync(approvalRequestId, ct);
        return Ok(result);
    }

    /// <summary>Preview only. Plain-English narrative built only from this request's own
    /// cycles/history/due-state. Writes nothing.</summary>
    [HttpPost("status-summary")]
    [ProducesResponseType(typeof(ApprovalAiStatusSummaryResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApprovalAiStatusSummaryResponseDto>> SummarizeStatus(long approvalRequestId, CancellationToken ct)
    {
        var result = await _approvalAi.SummarizeStatusAsync(approvalRequestId, ct);
        return Ok(result);
    }
}
