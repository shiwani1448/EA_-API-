namespace Jarvis5.Dtos.EaFms;

public class ApprovalDecisionDto
{
    public string? Comment { get; set; }
    /// <summary>Frontend-supplied actor snapshot: the operator's employee id (attribution; not verified).</summary>
    public string? EmployeeId { get; set; }
    /// <summary>Frontend-supplied actor snapshot: the operator's employee name. Becomes ApprovedBy / RejectedBy.</summary>
    public string? EmployeeName { get; set; }
}
