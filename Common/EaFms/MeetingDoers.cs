using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
namespace Jarvis5.Common.EaFms;
public static class MeetingDoers
{
    public static List<MeetingDoerDto> ToDtos(Meeting meeting) => meeting.DoerIds
        .Select((id, i) => new MeetingDoerDto { DoerId = id, DoerName = meeting.DoerNames[i] }).ToList();

    public static (string[] Ids, string[] Names) ToArrays(IEnumerable<MeetingDoerDto> doers)
    {
        var ids = new List<string>();
        var names = new List<string>();
        foreach (var doer in doers)
        {
            if (doer is null || string.IsNullOrWhiteSpace(doer.DoerId))
                throw new Jarvis5.Common.BadRequestException("Each doer must have a nonblank DoerId.");
            if (string.IsNullOrWhiteSpace(doer.DoerName))
                throw new Jarvis5.Common.BadRequestException("Each doer must have a nonblank DoerName.");

            ids.Add(doer.DoerId.Trim());
            names.Add(doer.DoerName.Trim());
        }

        return (ids.ToArray(), names.ToArray());
    }
}
