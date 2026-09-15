namespace Jarvis5.Dtos.EaFms;

public class ApprovalRequestDto
{
    public string? RequestTitle { get; set; }
    public string? RequestType { get; set; }
    public string? RequestedBy { get; set; }
    public string? Department { get; set; }
    public string? Priority { get; set; }
    public string? Description { get; set; }
    public string? Justification { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime? RequiredApprovalDate { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public string? CreatedBy { get; set; }
}
