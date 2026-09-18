using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[ApiController]
public class TravelHospitalityExecutionController(ITravelArrangementService service) : ControllerBase
{
    [HttpPost("api/ea/travel/requests/{travelRequestId:long}/hospitality")]
    [ProducesResponseType(typeof(TravelHospitalityResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long travelRequestId, [FromBody] SaveTravelHospitalityDto dto, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateHospitalityAsync(travelRequestId, dto, ct));
    [HttpGet("api/ea/travel/requests/{travelRequestId:long}/hospitality")]
    [ProducesResponseType(typeof(List<TravelHospitalityResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(long travelRequestId, CancellationToken ct) => Ok(await service.ListHospitalityAsync(travelRequestId, ct));
    [HttpGet("api/ea/travel/hospitality/{hospitalityId:long}")]
    [ProducesResponseType(typeof(TravelHospitalityResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(long hospitalityId, CancellationToken ct) => Ok(await service.GetHospitalityAsync(hospitalityId, ct));
    [HttpPut("api/ea/travel/hospitality/{hospitalityId:long}")]
    [ProducesResponseType(typeof(TravelHospitalityResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long hospitalityId, [FromBody] SaveTravelHospitalityDto dto, CancellationToken ct) =>
        Ok(await service.UpdateHospitalityAsync(hospitalityId, dto, ct));
}
