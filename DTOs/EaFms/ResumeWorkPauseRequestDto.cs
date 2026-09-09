using System;

namespace Jarvis5.Dtos.EaFms;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public class ResumeWorkPauseRequestDto
{
    /// <summary>
    /// Meaningful central status name for Continue: "In Progress" or "Submitted".
    /// </summary>
    public string? TargetStatusName { get; set; }

    public string? ResumedReason { get; set; }
}
