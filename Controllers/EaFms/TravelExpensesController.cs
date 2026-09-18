using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Jarvis5.Controllers.EaFms;

// Current EA access convention is unchanged. Actor attribution is not approval authorization.
[ApiController]
public class TravelExpensesController(ITravelExpenseService service) : ControllerBase
{
    [HttpPost("api/ea/travel/requests/{travelRequestId:long}/expenses")]
    [ProducesResponseType(typeof(TravelExpenseResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long travelRequestId, [FromBody] SaveTravelExpenseDto dto, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateAsync(travelRequestId, dto, ct));
    [HttpGet("api/ea/travel/requests/{travelRequestId:long}/expenses")]
    public async Task<IActionResult> List(long travelRequestId, CancellationToken ct) => Ok(await service.ListAsync(travelRequestId, ct));
    [HttpGet("api/ea/travel/expenses/{expenseId:long}")]
    public async Task<IActionResult> Get(long expenseId, CancellationToken ct) => Ok(await service.GetAsync(expenseId, ct));
    [HttpPut("api/ea/travel/expenses/{expenseId:long}")]
    public async Task<IActionResult> Update(long expenseId, [FromBody] SaveTravelExpenseDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(expenseId, dto, ct));
    [HttpPost("api/ea/travel/expenses/{expenseId:long}/submit")]
    public async Task<IActionResult> Submit(long expenseId, CancellationToken ct) => Ok(await service.SubmitAsync(expenseId, ct));
    [HttpPost("api/ea/travel/expenses/{expenseId:long}/approve")]
    public async Task<IActionResult> Approve(long expenseId, CancellationToken ct) => Ok(await service.ApproveAsync(expenseId, ct));
    [HttpPost("api/ea/travel/expenses/{expenseId:long}/reject")]
    public async Task<IActionResult> Reject(long expenseId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectTravelExpenseDto? dto, CancellationToken ct) =>
        Ok(await service.RejectAsync(expenseId, dto, ct));
    [HttpGet("api/ea/travel/requests/{travelRequestId:long}/expense-summary")]
    public async Task<IActionResult> Summary(long travelRequestId, CancellationToken ct) => Ok(await service.SummaryAsync(travelRequestId, ct));
}
