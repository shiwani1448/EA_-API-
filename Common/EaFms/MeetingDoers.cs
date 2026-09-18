using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
namespace Jarvis5.Common.EaFms;
public static class MeetingDoers
{
    public static List<MeetingDoerDto> ToDtos(Meeting meeting) => meeting.DoerIds
        .Select((id, i) => new MeetingDoerDto { DoerId = id, DoerName = meeting.DoerNames[i] }).ToList();

    /// <summary>
    /// DoerId/DoerName are frontend-owned form fields — neither is backend-mandatory.
    /// A null/blank value is stored as "" (Meeting.DoerIds/DoerNames are parallel
    /// non-nullable string[] columns, so this is the storage-permitted representation
    /// of "no value supplied", not a fabricated identity). DoerId and DoerName are
    /// never copied into one another.
    /// </summary>
    public static (string[] Ids, string[] Names) ToArrays(IEnumerable<MeetingDoerDto> doers)
    {
        var ids = new List<string>();
        var names = new List<string>();
        foreach (var doer in doers)
        {
            if (doer is null) continue;
            ids.Add(doer.DoerId?.Trim() ?? string.Empty);
            names.Add(doer.DoerName?.Trim() ?? string.Empty);
        }

        return (ids.ToArray(), names.ToArray());
    }
}
