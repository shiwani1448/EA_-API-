namespace Jarvis5.Dtos.DevelopmentPlan;

/// <summary>Returned by Create/Update/Get — field names mirror the SCIH_Task table
/// columns exactly, so the same shape appears whether the module was just created,
/// updated, or fetched via GET /api/development-plan/{requestId}.</summary>
public class TaskModuleDetailDto
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public string ApprovedId { get; set; } = string.Empty;
    public string SolutionId { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>Whatever JSON is stored in SCIH_Task.StageDetailsJson, returned as-is —
    /// the create endpoint stores stage entries verbatim from the client with no
    /// fixed schema, so this isn't deserialized into a typed shape.</summary>
    public object StageDetails { get; set; } = new List<object>();
    public string DoerLead { get; set; } = string.Empty;
    public DateTime OverallStartDate { get; set; }
    public DateTime OverallEndDate { get; set; }
    public string Priority { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdationDate { get; set; }
    public string? UpdatedBy { get; set; }
    public string IsDelete { get; set; } = "false";
    public string? IsDeletedBy { get; set; }
}
