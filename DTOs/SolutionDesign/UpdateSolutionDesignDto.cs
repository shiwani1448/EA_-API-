using Jarvis5.Dtos;

namespace Jarvis5.Dtos.SolutionDesign;

/// <summary>Body for PUT /api/request/{requestId}/solution-design — the user-edited
/// version of the AI-generated solution design, exactly as reviewed on the frontend.</summary>
public class UpdateSolutionDesignDto
{
    public SolutionDesignResultDto Solution { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
    public string? Remarks { get; set; }
}
