namespace Jarvis5.Common;

/// <summary>
/// Stage numbers for the full SCIH lifecycle. Only Stage 1 (Request Raised) is
/// implemented today; later stages are reserved here so SCIH_Request /
/// SCIH_RequestHistory need no schema changes when they are built.
/// </summary>
public static class SCIHStage
{
    public const int RequestRaised = 1;
    public const int Analysis = 2;
    public const int SolutionDesign = 3;
    public const int Approval = 4;
    public const int Development = 5;
    public const int TestingAndRRR = 6;
    public const int Deployment = 7;
    public const int Maintenance = 8;

    public static string NameOf(int stage) => stage switch
    {
        RequestRaised => "Request Raised",
        Analysis => "Analysis",
        SolutionDesign => "Solution Design",
        Approval => "Approval",
        Development => "Development",
        TestingAndRRR => "Testing & RRR",
        Deployment => "Deployment",
        Maintenance => "Maintenance",
        _ => "Unknown"
    };
}

public static class SCIHStatus
{
    public const string Raised = "RAISED";
    public const string Analysis = "ANALYSIS";
    public const string SolutionDesign = "SOLUTION_DESIGN";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string AnalysisRework = "ANALYSIS_REWORK";
    public const string AnalysisReworkCompleted = "ANALYSIS_REWORK_COMPLETED";
    public const string Development = "DEVELOPMENT";
    public const string Deleted = "DELETED";
}

/// <summary>Per-version generation status on a single SCIH_Analysis row. Only
/// Draft (DB default) / Completed / ReworkCompleted are written today — Generating
/// and Rework are reserved for a future async-generation flow.</summary>
public static class SCIHAnalysisStatus
{
    public const string Draft = "DRAFT";
    public const string Generating = "GENERATING";
    public const string Completed = "COMPLETED";
    public const string Rework = "REWORK";
    public const string ReworkCompleted = "REWORK_COMPLETED";
    public const string Approved = "APPROVED";
}

/// <summary>Decision recorded on a single SCIH_Approval round.</summary>
public static class SCIHApprovalDecision
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

/// <summary>Lifecycle of a single SCIH_SolutionDesign row: Draft (fresh AI output,
/// untouched) -> Reviewed (user has edited it) -> Approved (locked, moves the
/// request to Stage 3).</summary>
public static class SCIHSolutionDesignStatus
{
    public const string Draft = "Draft";
    public const string Reviewed = "Reviewed";
    public const string Approved = "Approved";
}

public static class SCIHPriority
{
    public static readonly string[] Allowed = { "LOW", "MEDIUM", "HIGH", "CRITICAL" };
}

/// <summary>Parent-entity kinds the shared Attachment module can attach files to.
/// Also doubles as the folder name (title-cased) under Content/ where that
/// entity's files are stored — see SCIHAttachmentEntityType.FolderName.</summary>
public static class SCIHAttachmentEntityType
{
    public const string Request = "REQUEST";
    public const string Solution = "SOLUTION";
    public const string Development = "DEVELOPMENT";

    public static readonly string[] Allowed = { Request, Solution, Development };

    public static string FolderName(string entityType) => entityType switch
    {
        Request => "Request",
        Solution => "Solution",
        Development => "Development",
        _ => entityType
    };
}

/// <summary>Lifecycle of a single stage entry inside a SCIH_Task row's StageDetailsJson.</summary>
public static class SCIHTaskStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "In Progress";
    public const string Completed = "Completed";
    public const string OnHold = "On Hold";

    public static readonly string[] Allowed = { Pending, InProgress, Completed, OnHold };
}

/// <summary>Lifecycle of a single SCIH_SnagList row. Open is the initial state
/// (snag raised); Closed is only ever set by POST /api/snaglist/{id}/close once
/// every selected stage — including the mandatory Final Feedback, Technical
/// Documentation, AI Documentation Review and User Training Documentation
/// stages — is Completed.</summary>
public static class SCIHSnagStatus
{
    public const string Open = "Open";
    public const string InProgress = "In Progress";
    public const string Completed = "Completed";
    public const string Closed = "Closed";

    public static readonly string[] Allowed = { Open, InProgress, Completed, Closed };
}

public static class SCIHHistoryAction
{
    public const string RequestCreated = "Request Created";
    public const string RequestUpdated = "Request Updated";
    public const string AttachmentAdded = "Attachment Added";
    public const string AttachmentRemoved = "Attachment Removed";
    public const string PainPointAdded = "Pain Point Added";
    public const string PainPointUpdated = "Pain Point Updated";
    public const string PainPointRemoved = "Pain Point Removed";
    public const string RequestDeleted = "Request Deleted";

    public const string AnalysisGenerated = "Analysis Generated";
    public const string AnalysisSaved = "Analysis Saved";
    public const string AnalysisUpdated = "Analysis Updated";
    public const string StageChanged = "Stage Changed";

    public const string SolutionDesignGenerated = "Solution Design Generated";
    public const string SolutionDesignUpdated = "Solution Design Updated";
    public const string SolutionDesignApproved = "Solution Design Approved";

    public const string AnalysisReworked = "Analysis Reworked";
    public const string SubmittedForApproval = "Submitted For Approval";
    public const string RequestApproved = "Request Approved";
    public const string RequestRejected = "Request Rejected";
    public const string RequestStatusSynced = "Request Status Synced";

    public const string TaskModuleCreated = "Task Module Created";
    public const string TaskModuleUpdated = "Task Module Updated";
    public const string TaskModuleDeleted = "Task Module Deleted";
    public const string TaskStageAdded = "Task Stage Added";
    public const string TaskStageRemoved = "Task Stage Removed";
    public const string TaskStageUpdated = "Task Stage Updated";

    public const string SnagListCreated = "Snag List Created";
    public const string SnagListUpdated = "Snag List Updated";
    public const string SnagStageUpdated = "Snag Stage Updated";
    public const string SnagListClosed = "Snag List Closed";
}
