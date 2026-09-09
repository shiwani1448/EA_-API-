namespace Jarvis5.Dtos.DevelopmentPlan;

/// <summary>Body for POST /api/development-plan/{requestId} — creates one
/// SCIH_Task row per module, each with its selected stages.</summary>
public class CreateDevelopmentPlanDto
{
    public List<CreateModuleDto> Modules { get; set; } = new();
}

/// <summary>Field names mirror every SCIH_Task table column, matching
/// TaskModuleDetailDto exactly (Id/RequestId excluded — those come from the
/// route/DB identity, not the body). ApprovedId, SolutionId, CurrentStage and the
/// audit columns are accepted for schema symmetry with the GET/response shape but
/// are always server-computed — any value sent for them is ignored.</summary>
public class CreateModuleDto
{
    public string Module { get; set; } = string.Empty;
    public string DoerLead { get; set; } = string.Empty;
    public DateTime OverallStartDate { get; set; }
    public DateTime OverallEndDate { get; set; }
    public string Priority { get; set; } = string.Empty;

    /// <summary>Untyped passthrough — no fixed schema, no server-side validation or
    /// SCIH_StageMaster lookup. Stored exactly as sent; the frontend owns its shape
    /// and correctness entirely.</summary>
    public List<object> StageDetails { get; set; } = new();

    /// <summary>Ignored — server sets this from the request's latest SCIH_Approval.Id.</summary>
    public string ApprovedId { get; set; } = string.Empty;

    /// <summary>Ignored — server sets this from the request's SCIH_SolutionDesign.Id.</summary>
    public string SolutionId { get; set; } = string.Empty;

    /// <summary>Ignored — server snapshots the request's current stage name.</summary>
    public string CurrentStage { get; set; } = string.Empty;

    /// <summary>Ignored — new modules always start at SCIHTaskStatus.Pending.</summary>
    public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>Ignored — server sets this to the current UTC time.</summary>
    public DateTime CreationDate { get; set; }

    /// <summary>Ignored — server sets this from the acting user.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Ignored — always null on create.</summary>
    public DateTime? UpdationDate { get; set; }

    /// <summary>Ignored — always null on create.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Ignored — new modules always start with IsDelete = "false".</summary>
    public string IsDelete { get; set; } = "false";

    /// <summary>Ignored — always null on create.</summary>
    public string? IsDeletedBy { get; set; }
}
