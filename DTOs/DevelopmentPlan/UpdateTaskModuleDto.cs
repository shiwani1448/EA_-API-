namespace Jarvis5.Dtos.DevelopmentPlan;

/// <summary>Body for PATCH /api/development-plan/{taskId} — partial update: only fields
/// present (non-null) in the request are changed, everything else keeps its current
/// value. Field names mirror every SCIH_Task table column, matching
/// TaskModuleDetailDto/CreateModuleDto exactly. ApprovedId, SolutionId, CurrentStage,
/// CurrentStatus and the audit columns are accepted for schema symmetry but are never
/// applied — any value sent for them is ignored (Module/DoerLead/OverallStartDate/
/// OverallEndDate/Priority/StageDetails are the only fields this endpoint actually
/// changes). StageDetails: omit/null to leave stages untouched; if provided, it's
/// untyped passthrough — no fixed schema, no SCIH_StageMaster lookup, no add/removed
/// history tracking, no "at least one selected" check — stored exactly as sent,
/// replacing the module's full stage list in one go.</summary>
public class UpdateTaskModuleDto
{
    public string? Module { get; set; }
    public string? DoerLead { get; set; }
    public DateTime? OverallStartDate { get; set; }
    public DateTime? OverallEndDate { get; set; }
    public string? Priority { get; set; }
    public List<object>? StageDetails { get; set; }

    /// <summary>Ignored — set once at creation, never changed here.</summary>
    public string ApprovedId { get; set; } = string.Empty;

    /// <summary>Ignored — set once at creation, never changed here.</summary>
    public string SolutionId { get; set; } = string.Empty;

    /// <summary>Ignored — set once at creation, never changed here.</summary>
    public string CurrentStage { get; set; } = string.Empty;

    /// <summary>Ignored — this endpoint doesn't change module status.</summary>
    public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>Ignored — immutable once created.</summary>
    public DateTime CreationDate { get; set; }

    /// <summary>Ignored — immutable once created.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Ignored — server always sets this to the current UTC time.</summary>
    public DateTime? UpdationDate { get; set; }

    /// <summary>Ignored — server always sets this from the acting user.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Ignored — soft delete is only ever set by DELETE /api/development-plan/{taskId}.</summary>
    public string IsDelete { get; set; } = "false";

    /// <summary>Ignored — soft delete is only ever set by DELETE /api/development-plan/{taskId}.</summary>
    public string? IsDeletedBy { get; set; }
}
