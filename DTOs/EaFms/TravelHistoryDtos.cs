namespace Jarvis5.Dtos.EaFms;

/// <summary>
/// One chronological Travel timeline entry, read entirely from the existing shared
/// ea_audit_logs infrastructure — no dedicated Travel history table.
/// TravelRequestCycle.Id and WorkflowInstanceId are deliberately not exposed here;
/// CycleNo is the business-facing approval/rework cycle number (1, 2, 3, ...).
/// </summary>
public class TravelHistoryEventDto
{
    public long AuditId { get; set; }
    public long TravelRequestId { get; set; }
    public string TravelReferenceNo { get; set; } = string.Empty;
    public long EaTaskId { get; set; }
    /// <summary>Business approval/rework cycle number, or null when not associated with one.</summary>
    public int? CycleNo { get; set; }
    public string Action { get; set; } = string.Empty;
    /// <summary>One of: TravelBusiness, TravelApproval, Booking, Expense, LocalTransport, Hospitality — or null.</summary>
    public string? StateType { get; set; }
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime PerformedAt { get; set; }
    public string? Comment { get; set; }
    public Dictionary<string, object?>? Metadata { get; set; }
}
