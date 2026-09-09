namespace Jarvis5.Entities;

/// <summary>One row per Snag List (RRR) raised against a Task module once Testing
/// finds a defect/pending item — Stage 6 (Testing & RRR) counterpart of SCIHTask.
/// Same column-type convention as SCIHTask: only Id is numeric, StageDetails is
/// JSON, everything else (including dates and the delete flag) is plain text.
/// No DB-level foreign keys: RequestId/TaskId are plain string snapshots.</summary>
public class SCIHSnagList
{
    public int Id { get; set; }

    /// <summary>SCIH_Request.Id, stored as text (no DB-level FK — see class summary).
    /// Optional: a Snag List can be raised standalone with no request link.</summary>
    public string? RequestId { get; set; }

    /// <summary>SCIH_Task.Id of the Development Planning module this snag was raised
    /// against, stored as text (no DB-level FK — see class summary). Optional: a
    /// Snag List can be raised standalone with no task link.</summary>
    public string? TaskId { get; set; }

    public string Module { get; set; } = string.Empty;

    public string SnagDescription { get; set; } = string.Empty;

    /// <summary>LOW | MEDIUM | HIGH | CRITICAL — see SCIHPriority.</summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>Open | In Progress | Completed | Closed — see SCIHSnagStatus.</summary>
    public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>Raw JSON array of the selected stages for this snag list — see SnagStageDetailDto.</summary>
    public string StageDetails { get; set; } = "[]";

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
