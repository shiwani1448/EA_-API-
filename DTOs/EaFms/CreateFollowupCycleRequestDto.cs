using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateFollowupCycleRequestDto
{
    public string? Note { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public string? OutcomeCode { get; set; }
}
