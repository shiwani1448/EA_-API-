namespace Jarvis5.Dtos.Approval;

/// <summary>Body for POST /api/request/{requestId}/reject. Rejection Reason,
/// Improvement Areas and Comments are all mandatory — the Director cannot reject
/// without completing them.</summary>
public class RejectRequestDto
{
    public long ApprovedBy { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
    public List<string> ImprovementAreas { get; set; } = new();
    public string Comments { get; set; } = string.Empty;
}
