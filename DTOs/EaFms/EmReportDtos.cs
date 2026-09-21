namespace Jarvis5.Dtos.EaFms;

/// <summary>Read-only current-state EM overview filters. The date cohort is EaTask.CreatedDate.</summary>
public class EmReportOverviewQueryDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public long? BusinessModuleId { get; set; }
}

/// <summary>Current-state KPIs for the selected EaTask-created-date cohort.</summary>
public class EmReportOverviewResponseDto
{
    public int TotalTasks { get; set; }
    public int NotStarted { get; set; }
    public int InProgress { get; set; }
    public int Paused { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int OnTimeCompleted { get; set; }
    public int DelayedCompleted { get; set; }
    public int NotMeasuredCompleted { get; set; }
    public decimal OnTimeCompletionPercentage { get; set; }
    public int DelayedTasks { get; set; }
}

/// <summary>Current management-attention counts for the selected EaTask created-date cohort.</summary>
public class EmReportAttentionResponseDto
{
    public int DueToday { get; set; }
    public int Delayed { get; set; }
    public int TasksRequiringFollowup { get; set; }
    public int OpenEscalations { get; set; }
}

/// <summary>One row per BusinessModule that has at least one task in the selected created-date cohort (current state).</summary>
public class EmReportModuleSummaryDto
{
    public long BusinessModuleId { get; set; }
    /// <summary>Current BusinessModule master name (not the EaTask snapshot).</summary>
    public string ModuleName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int NotStarted { get; set; }
    public int InProgress { get; set; }
    public int Paused { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int OnTimeCompleted { get; set; }
    public int DelayedCompleted { get; set; }
    public int NotMeasuredCompleted { get; set; }
    public decimal OnTimeCompletionPercentage { get; set; }
    public int Delayed { get; set; }
    /// <summary>Distinct source tasks with at least one incomplete Followup (not Followup rows).</summary>
    public int TasksRequiringFollowup { get; set; }
    /// <summary>Unresolved Escalation rows reachable through the module's cohort Followups.</summary>
    public int OpenEscalations { get; set; }
}

/// <summary>Task Register filters: the shared cohort filters (EaTask.CreatedDate, module) plus register-only filters and paging.</summary>
public class EmReportTaskRegisterQueryDto : EmReportOverviewQueryDto
{
    /// <summary>Display state: NotStarted | InProgress | Paused | Completed | Cancelled. Paused is derived (InProgress with an open pause).</summary>
    public string? ExecutionStatus { get; set; }
    /// <summary>OnTime | Delayed | NotMeasured (the shared EM classification).</summary>
    public string? Performance { get; set; }
    /// <summary>true: at least one incomplete Followup; false: none (no Followup, or all completed).</summary>
    public bool? RequiresFollowup { get; set; }
    /// <summary>true: at least one unresolved Escalation; false: none.</summary>
    public bool? HasOpenEscalation { get; set; }
    /// <summary>Case-insensitive contains over task title, BusinessRecordId and current module name.</summary>
    public string? Search { get; set; }
    /// <summary>1-based page number (default 1).</summary>
    public int Page { get; set; } = 1;
    /// <summary>Rows per page (default 50, maximum 200).</summary>
    public int PageSize { get; set; } = 50;
}

/// <summary>One existing EaTask behind the EM numbers (current state).</summary>
public class EmReportTaskRowDto
{
    /// <summary>ea_tasks.Id.</summary>
    public long EaTaskId { get; set; }
    public long BusinessModuleId { get; set; }
    /// <summary>Current BusinessModule master name.</summary>
    public string ModuleName { get; set; } = string.Empty;
    /// <summary>Business source id (Approval: ReferenceNo; others: the numeric id as text).</summary>
    public string BusinessRecordId { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    /// <summary>Persisted: NotStarted | InProgress | Completed | Cancelled.</summary>
    public string ExecutionStatus { get; set; } = string.Empty;
    /// <summary>Derived; true only while ExecutionStatus is InProgress with an open pause.</summary>
    public bool IsPaused { get; set; }
    /// <summary>Approval RequiredApprovalDate, Travel RequiredDate, Delegation DueDate; null for Meeting and when unavailable.</summary>
    public DateTime? DueDate { get; set; }
    /// <summary>OnTime | Delayed | NotMeasured.</summary>
    public string Performance { get; set; } = "NotMeasured";
    /// <summary>NoFollowup | Pending | Completed.</summary>
    public string FollowupStatus { get; set; } = "NoFollowup";
    /// <summary>Incomplete non-deleted Followup rows for this task.</summary>
    public int PendingFollowupCount { get; set; }
    /// <summary>Unresolved non-deleted Escalation rows through this task's non-deleted Followups.</summary>
    public int OpenEscalationCount { get; set; }
    /// <summary>Null when no TAT applies.</summary>
    public int? AllottedTatMinutes { get; set; }
    /// <summary>Frozen value when completed, current canonical value while active; null when no TAT applies (or cancelled).</summary>
    public int? CurrentOrFinalTatUsedMinutes { get; set; }
}
