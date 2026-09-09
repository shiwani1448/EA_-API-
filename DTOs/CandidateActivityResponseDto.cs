using System.Text.Json;

namespace hrms_api.DTOs;

public class CandidateActivityResponseDto
{
    public int Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? TotalScore { get; set; }
    public JsonDocument? EvaluationJson { get; set; }
    public string? Remarks { get; set; }
    public DateTime ActionDate { get; set; }
    public int? CreatedBy { get; set; }
}
