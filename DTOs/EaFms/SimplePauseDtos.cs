using System.Text.Json.Serialization;

namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class SimplePauseRequestDto
{
    public string Remark { get; set; } = string.Empty;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class SimpleResumeRequestDto
{
    public string? Remark { get; set; }
}

public class SimplePauseResponseDto
{
    public long PauseId { get; set; }
    public string? Remark { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public WorkflowResponseDto Workflow { get; set; } = null!;
}
