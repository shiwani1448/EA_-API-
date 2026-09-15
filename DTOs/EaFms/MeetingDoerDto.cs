using System.Text.Json.Serialization;
namespace Jarvis5.Dtos.EaFms;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class MeetingDoerDto
{
    public string DoerId { get; set; } = string.Empty;
    public string DoerName { get; set; } = string.Empty;
}
