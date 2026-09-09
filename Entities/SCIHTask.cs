namespace Jarvis5.Entities;

/// <summary>One row per project module under a request's Development Planning
/// phase (Stage 5). Column set/types match the exact Task table spec: only Id is
/// numeric (int) and StageDetails is JSON — every other column is plain text,
/// including dates (ISO-8601 string) and the delete flag ("true"/"false" string).
/// No DB-level foreign keys: RequestId/ApprovedId/SolutionId are plain string
/// snapshots, not enforced relations.</summary>
public class SCIHTask
{
    public int Id { get; set; }

    /// <summary>SCIH_Request.Id, stored as text (no DB-level FK — see class summary).</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>SCIH_Approval.Id of the request's latest approval round, snapshotted
    /// when the module is created. Empty string if the request has no approval round.</summary>
    public string ApprovedId { get; set; } = string.Empty;

    /// <summary>SCIH_SolutionDesign.Id for the request, snapshotted when the module
    /// is created. Empty string if the request has no solution design.</summary>
    public string SolutionId { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    /// <summary>Name of the SCIH_Request stage in effect when this module was created
    /// (e.g. "Development") — see SCIHStage.NameOf.</summary>
    public string CurrentStage { get; set; } = string.Empty;

    /// <summary>Pending | In Progress | Completed | On Hold — see SCIHTaskStatus.</summary>
    public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>Raw JSON array of the selected stages for this module — see StageDetailDto.</summary>
    public string StageDetails { get; set; } = "[]";

    public string DoerLead { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC, e.g. "2026-08-01T10:00:00Z".</summary>
    public string OverallStartDate { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC, e.g. "2026-08-10T18:00:00Z".</summary>
    public string OverallEndDate { get; set; } = string.Empty;

    /// <summary>LOW | MEDIUM | HIGH | CRITICAL — see SCIHPriority.</summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC.</summary>
    public string CreationDate { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>ISO-8601 UTC.</summary>
    public string? UpdationDate { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>"true" | "false".</summary>
    public string IsDelete { get; set; } = "false";

    public string? IsDeletedBy { get; set; }
}
