namespace Jarvis5.Dtos.Analysis;

/// <summary>Body for POST /api/request/{requestId}/analysis — the user-edited
/// version of the AI analysis, exactly as reviewed on the frontend.</summary>
public class SaveAnalysisDto
{
    public AiAnalysisResultDto Analysis { get; set; } = new();
    public string? Remarks { get; set; }
}
