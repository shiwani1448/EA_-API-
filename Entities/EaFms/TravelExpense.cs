namespace Jarvis5.Entities.EaFms;

// Explicit expense ledger, independent of request estimates and operational costs.
public class TravelExpense
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }
    public TravelRequest TravelRequest { get; set; } = null!;
    public Attachment? ReceiptAttachment { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime? ExpenseDate { get; set; }
    public long? ReceiptAttachmentId { get; set; }
    public string Status { get; set; } = "Draft";
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
