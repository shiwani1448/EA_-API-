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
    /// <summary>The designated approver (who should decide). Not the person who actually approved.</summary>
    public string? ApproverName { get; set; }
    /// <summary>Who actually approved (business data). The approve action sets it authoritatively.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? ApprovedBy { get; set; }
    /// <summary>Who actually rejected (business data). The reject action sets it authoritatively.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? RejectedBy { get; set; }
    public string? CreatedBy { get; set; }
}
