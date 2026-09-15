namespace Jarvis5.Entities.EaFms;

public class ApprovalRequest
{
    public long Id { get; set; }
    // Link to central EA task row (ea_tasks.Id). Must be non-nullable in schema.
    public long EaTaskId { get; set; }
    public EaTask EaTask { get; set; } = null!;
    public string ReferenceNo { get; set; } = string.Empty;
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
    public string? WorkflowStatus { get; set; }
    public int CurrentCycleNo { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public bool IsDeleted { get; set; }
}
