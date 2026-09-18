using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

// Same EA access convention as TravelRequestsController. No selected-approver policy exists.
[ApiController]
public class TravelApprovalsController(ITravelApprovalQueryService service) : ControllerBase
{
    [HttpGet("api/ea/travel/approvals/pending")]
    [ProducesResponseType(typeof(PagedResult<TravelPendingApprovalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pending([FromQuery] TravelPendingApprovalQueryDto query, CancellationToken ct) =>
        Ok(await service.PendingAsync(query, ct));

    [HttpGet("api/ea/travel/requests/{travelRequestId:long}/approval")]
    [ProducesResponseType(typeof(TravelApprovalDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(long travelRequestId, CancellationToken ct) =>
        Ok(await service.GetAsync(travelRequestId, ct));
}
