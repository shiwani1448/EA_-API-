namespace Jarvis5.Dtos.Approval;

/// <summary>Body for POST /api/request/{requestId}/approve.</summary>
public class ApproveRequestDto
{
    public long ApprovedBy { get; set; }
    public string? Comments { get; set; }
}
