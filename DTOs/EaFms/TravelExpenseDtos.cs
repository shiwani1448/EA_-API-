namespace Jarvis5.Dtos.EaFms;

// PUT replaces financial fields. Parent, status, and actor fields are server-owned.
public class SaveTravelExpenseDto
{
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public DateTime? ExpenseDate { get; set; }
    public long? ReceiptAttachmentId { get; set; }
}

public class RejectTravelExpenseDto
{
    public string? RejectionReason { get; set; }
}

public class TravelExpenseResponseDto
{
    public long ExpenseId { get; set; }
    public long TravelRequestId { get; set; }
    public string TravelReferenceNo { get; set; } = string.Empty;
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
    public TravelDocumentResponseDto? Receipt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TravelExpenseEstimatedDto
{
    public string? Currency { get; set; }
    public decimal? Travel { get; set; }
    public decimal? Hotel { get; set; }
    public decimal? LocalTransport { get; set; }
    public decimal? Hospitality { get; set; }
    public decimal Total { get; set; }
}

public class TravelExpenseActualDto
{
    public decimal Travel { get; set; }
    public decimal Hotel { get; set; }
    public decimal LocalTransport { get; set; }
    public decimal Hospitality { get; set; }
    public decimal Other { get; set; }
    public decimal Total { get; set; }
}

public class TravelExpenseCurrencySummaryDto
{
    // Null means unspecified currency; never assumed to equal the request currency.
    public string? Currency { get; set; }
    public TravelExpenseActualDto Actual { get; set; } = new();
    public decimal DraftTotal { get; set; }
    public decimal SubmittedTotal { get; set; }
    public decimal ApprovedTotal { get; set; }
    public decimal RejectedTotal { get; set; }
}

public class TravelExpenseSummaryDto
{
    public long TravelRequestId { get; set; }
    public string TravelReferenceNo { get; set; } = string.Empty;
    public TravelExpenseEstimatedDto Estimated { get; set; } = new();
    public List<TravelExpenseCurrencySummaryDto> ActualByCurrency { get; set; } = new();
}
