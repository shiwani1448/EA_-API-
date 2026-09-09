using System.Text.Json.Serialization;
namespace Jarvis5.Dtos.EaFms;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class StartWorkRequestDto
{
    public string? Notes { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateWaitingRequestDto : CreateWorkPauseRequestDto
{
    public DateTime? NextFollowupAt { get; set; }
    public string? CurrentRemarks { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CompleteWorkRequestDto
{
    public string Notes { get; set; } = string.Empty;
    public List<long> EvidenceAttachmentIds { get; set; } = new();
}

public class WaitingResponseDto : WorkPauseResponseDto
{
    public DateTime? LastFollowupAt { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public string? CurrentRemarks { get; set; }
}

public class WorkflowWaitingResponseDto
{
    public WorkflowResponseDto Workflow { get; set; } = null!;
    public WaitingResponseDto Waiting { get; set; } = null!;
}

public class WorkflowWaitingListResponseDto
{
    public WorkflowResponseDto Workflow { get; set; } = null!;
    public List<WaitingResponseDto> Items { get; set; } = new();
}

public class CompleteWorkResponseDto
{
    public WorkflowResponseDto Workflow { get; set; } = null!;
    public List<long> EvidenceAttachmentIds { get; set; } = new();
}
