using Jarvis5.Dtos.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

[ApiController]
[Route("api/ea/meetings/{meetingId:long}/delegation")]
public class MeetingsDelegationController(
    MeetingDelegationService delegations, IMeetingService meetings, ICurrentUserService user) : ControllerBase
{
    [HttpPost("decline")]
    [ProducesResponseType(typeof(MeetingDetailResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MeetingDetailResponseDto>> Decline(long meetingId, CancellationToken ct)
    {
        await delegations.DeclineAsync(meetingId, user.UserName ?? user.UserId.ToString(), ct);
        return Ok(await meetings.GetByIdAsync(meetingId, ct));
    }
}
