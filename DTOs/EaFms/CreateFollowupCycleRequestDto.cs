using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateFollowupCycleRequestDto
{
    public string? Note { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee id). Stored as attribution; not verified.</summary>
    public string? EmployeeId { get; set; }
    /// <summary>Frontend-supplied actor snapshot (operator's employee name). Stored as attribution; not verified.</summary>
    public string? EmployeeName { get; set; }
    public DateTime? ExpectedResponseAt { get; set; }
    public string? OutcomeCode { get; set; }
}
