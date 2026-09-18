using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[ApiController]
public class TravelBookingsController(ITravelBookingService service) : ControllerBase
{
    [HttpPost("api/ea/travel/requests/{travelRequestId:long}/bookings")]
    public async Task<IActionResult> Create(long travelRequestId, [FromBody] CreateTravelBookingDto dto, CancellationToken ct)
    {
        var result = await service.CreateAsync(travelRequestId, dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
    [HttpGet("api/ea/travel/requests/{travelRequestId:long}/bookings")]
    public async Task<IActionResult> List(long travelRequestId, CancellationToken ct) => Ok(await service.ListAsync(travelRequestId, ct));
    [HttpPut("api/ea/travel/bookings/{bookingId:long}")]
    public async Task<IActionResult> Update(long bookingId, [FromBody] UpdateTravelBookingDto dto, CancellationToken ct) =>
        Ok(await service.UpdateAsync(bookingId, dto, ct));
    [HttpPost("api/ea/travel/bookings/{bookingId:long}/cancel")]
    public async Task<IActionResult> Cancel(long bookingId, CancellationToken ct) => Ok(await service.CancelAsync(bookingId, ct));
}
