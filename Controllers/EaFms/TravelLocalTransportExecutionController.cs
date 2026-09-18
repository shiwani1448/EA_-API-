using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[ApiController]
public class TravelLocalTransportExecutionController(ITravelArrangementService service) : ControllerBase
{
    [HttpPost("api/ea/travel/requests/{travelRequestId:long}/local-transports")]
    [ProducesResponseType(typeof(TravelLocalTransportResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(long travelRequestId, [FromBody] SaveTravelLocalTransportDto dto, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.CreateLocalTransportAsync(travelRequestId, dto, ct));
    [HttpGet("api/ea/travel/requests/{travelRequestId:long}/local-transports")]
    [ProducesResponseType(typeof(List<TravelLocalTransportResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(long travelRequestId, CancellationToken ct) => Ok(await service.ListLocalTransportAsync(travelRequestId, ct));
    [HttpGet("api/ea/travel/local-transports/{localTransportId:long}")]
    [ProducesResponseType(typeof(TravelLocalTransportResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(long localTransportId, CancellationToken ct) => Ok(await service.GetLocalTransportAsync(localTransportId, ct));
    [HttpPut("api/ea/travel/local-transports/{localTransportId:long}")]
    [ProducesResponseType(typeof(TravelLocalTransportResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long localTransportId, [FromBody] SaveTravelLocalTransportDto dto, CancellationToken ct) =>
        Ok(await service.UpdateLocalTransportAsync(localTransportId, dto, ct));
}
