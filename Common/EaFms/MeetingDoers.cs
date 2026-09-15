using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
namespace Jarvis5.Common.EaFms;
public static class MeetingDoers
{
    public static List<MeetingDoerDto> ToDtos(Meeting meeting) => meeting.DoerIds
        .Select((id, i) => new MeetingDoerDto { DoerId = id, DoerName = meeting.DoerNames[i] }).ToList();
}
