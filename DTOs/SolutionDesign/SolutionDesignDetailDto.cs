using Jarvis5.Dtos;

namespace Jarvis5.Dtos.SolutionDesign;

public class SolutionDesignDetailDto
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public SolutionDesignResultDto Solution { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
    public string? AIModel { get; set; }
    public string? PromptVersion { get; set; }
    public DateTime GeneratedAt { get; set; }
    public long GeneratedBy { get; set; }
    public bool IsEdited { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public int CurrentStage { get; set; }
    public string CurrentStageName { get; set; } = string.Empty;
    public string RequestStatus { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
