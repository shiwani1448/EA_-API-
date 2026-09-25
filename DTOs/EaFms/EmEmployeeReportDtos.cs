namespace Jarvis5.Dtos.EaFms;

// ============================================================================================
// EM Employee Report — per-employee, per-week view across every EA FMS module.
//
// A "work item" is one unit of work: a Delegation or Approval phase (Actual / Review / Rework,
// all counted for the doer), a Meeting (per doer), a Follow-up, or a Travel request.
// A work item belongs to the ISO week of its planned date (India time), Monday–Saturday.
// ============================================================================================

/// <summary>Who and which week. Employee filters are optional: leave both empty for the whole-team view.</summary>
public class EmEmployeeReportQueryDto
{
    /// <summary>HRMS employee id (e.g. S5I-1013). Matches Delegation/Follow-up doer and Meeting doers.</summary>
    public string? EmployeeId { get; set; }
    /// <summary>Employee display name. Used for Approval and Travel (they store only a name) and when no id is given.</summary>
    public string? EmployeeName { get; set; }
    /// <summary>ISO week-based year. Defaults to the current India week.</summary>
    public int? Year { get; set; }
    /// <summary>ISO week number (1–53). Week 39 of 2026 = Mon 21 Sep – Sat 26 Sep. Defaults to the current India week.</summary>
    public int? Week { get; set; }
}

public class EmEmployeeTrendQueryDto : EmEmployeeReportQueryDto
{
    /// <summary>Number of weeks ending with the selected week (1–12, default 6).</summary>
    public int Weeks { get; set; } = 6;
}

public class EmWorkItemQueryDto : EmEmployeeReportQueryDto
{
    /// <summary>true: ignore Year/Week and list every week (e.g. all overdue work).</summary>
    public bool AllWeeks { get; set; }
    /// <summary>Delegation | Approval | Meeting | Follow-up | Travel.</summary>
    public string? Module { get; set; }
    /// <summary>Actual | Review | Rework | Meeting.</summary>
    public string? TaskType { get; set; }
    /// <summary>NotStarted | InProgress | Paused | Completed | Cancelled.</summary>
    public string? Status { get; set; }
    /// <summary>OnTime | Delayed | Overdue | Pending | NotMeasured.</summary>
    public string? Performance { get; set; }
    /// <summary>Contains-match on title, reference and owner name.</summary>
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    /// <summary>1–200, default 50.</summary>
    public int PageSize { get; set; } = 50;
}

// ---------------------------------------------------------------- KPIs

public class EmKpiResponseDto
{
    public string? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public int Year { get; set; }
    public int Week { get; set; }
    /// <summary>Monday of the week (date only).</summary>
    public DateTime WeekStart { get; set; }
    /// <summary>Saturday of the week (date only).</summary>
    public DateTime WeekEnd { get; set; }

    /// <summary>The top matrix: Summary of all FMS + one column per task type.</summary>
    public EmKpiMatrixDto Matrix { get; set; } = new();
    /// <summary>The same numbers per module (Delegation, Approval, Meeting, Follow-up, Travel).</summary>
    public List<EmKpiModuleDto> Modules { get; set; } = new();
    public EmTaskStatusDto TaskStatus { get; set; } = new();
    public EmTatDto Tat { get; set; } = new();
    /// <summary>Open work planned before this week that is already overdue.</summary>
    public int CarryForwardOverdue { get; set; }
    public EmFocusAreasDto FocusAreas { get; set; } = new();
}

public class EmKpiMatrixDto
{
    public EmKpiCellDto Summary { get; set; } = new();
    public EmKpiCellDto Actual { get; set; } = new();
    public EmKpiCellDto Review { get; set; } = new();
    public EmKpiCellDto Rework { get; set; } = new();
    public EmKpiCellDto Meeting { get; set; } = new();
}

public class EmKpiModuleDto
{
    public string Module { get; set; } = string.Empty;
    public EmKpiCellDto Kpi { get; set; } = new();
}

/// <summary>One matrix column. Cancelled work is not counted as planned.</summary>
public class EmKpiCellDto
{
    /// <summary>Work planned in the week (the "Total Task" / "Current Week Planned").</summary>
    public int Planned { get; set; }
    /// <summary>Of those, completed ("Current Week Actual").</summary>
    public int Completed { get; set; }
    public int NotCompleted { get; set; }
    /// <summary>NotCompleted / Planned × 100.</summary>
    public decimal NotCompletedPct { get; set; }
    /// <summary>Completed within TAT / by due date.</summary>
    public int OnTime { get; set; }
    /// <summary>Completed late.</summary>
    public int Delayed { get; set; }
    /// <summary>Delayed / Completed × 100.</summary>
    public decimal DelayedPct { get; set; }
    /// <summary>Completed with no TAT and no due date.</summary>
    public int NotMeasured { get; set; }
    /// <summary>Not completed and already past its TAT or due date.</summary>
    public int Overdue { get; set; }
    /// <summary>Not completed and still within time.</summary>
    public int Pending { get; set; }
    /// <summary>Change versus the previous week.</summary>
    public EmKpiDeltaDto Delta { get; set; } = new();
}

public class EmKpiDeltaDto
{
    public int Planned { get; set; }
    public int Completed { get; set; }
    public int OnTime { get; set; }
    public int Delayed { get; set; }
}

/// <summary>Task status donut. Rework items are counted only under Rework.</summary>
public class EmTaskStatusDto
{
    public int Total { get; set; }
    public int OnTime { get; set; }
    public int Pending { get; set; }
    public int Delayed { get; set; }
    public int Overdue { get; set; }
    public int Rework { get; set; }
    public int NotMeasured { get; set; }
}

/// <summary>TAT totals over the week's work items that have a TAT.</summary>
public class EmTatDto
{
    public int MeasuredItems { get; set; }
    public int AllottedMinutes { get; set; }
    public int UsedMinutes { get; set; }
    /// <summary>Allotted − Used (negative = over).</summary>
    public int DifferenceMinutes { get; set; }
    public bool IsOver { get; set; }
}

public class EmFocusAreasDto
{
    public List<string> Strengths { get; set; } = new();
    public List<string> Improve { get; set; } = new();
}

// ---------------------------------------------------------------- Trends

public class EmTrendResponseDto
{
    public string? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public List<EmTrendWeekDto> Weeks { get; set; } = new();
}

public class EmTrendWeekDto
{
    public int Year { get; set; }
    public int Week { get; set; }
    /// <summary>"W38" — ready for chart labels.</summary>
    public string Label { get; set; } = string.Empty;
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public EmTrendPointDto Summary { get; set; } = new();
    public EmTrendPointDto Actual { get; set; } = new();
    public EmTrendPointDto Review { get; set; } = new();
    public EmTrendPointDto Rework { get; set; } = new();
    public EmTrendPointDto Meeting { get; set; } = new();
}

public class EmTrendPointDto
{
    public int Planned { get; set; }
    public int Completed { get; set; }
    public int OnTime { get; set; }
    public int Delayed { get; set; }
}

// ---------------------------------------------------------------- Work items

/// <summary>One unit of work from any module, in one shape.</summary>
public class EmWorkItemDto
{
    /// <summary>Stable key, e.g. "Delegation:12:Review:1".</summary>
    public string ItemKey { get; set; } = string.Empty;
    /// <summary>Delegation | Approval | Meeting | Follow-up | Travel.</summary>
    public string Module { get; set; } = string.Empty;
    /// <summary>Actual | Review | Rework | Meeting.</summary>
    public string TaskType { get; set; } = string.Empty;
    /// <summary>Review/Rework cycle number (0 for Actual and non-phase items).</summary>
    public int ReviewCycleNumber { get; set; }
    /// <summary>The module record id (DelegationId, ApprovalRequestId, MeetingId, FollowupId, TravelRequestId).</summary>
    public long RecordId { get; set; }
    public long? EaTaskId { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Title { get; set; }
    public string? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    /// <summary>The date that places the item in a week.</summary>
    public DateTime? PlannedDate { get; set; }
    public int? Year { get; set; }
    public int? Week { get; set; }
    /// <summary>Business due date, when the module has one.</summary>
    public DateTime? DueDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>NotStarted | InProgress | Paused | Completed | Cancelled.</summary>
    public string Status { get; set; } = string.Empty;
    public bool IsPaused { get; set; }
    /// <summary>OnTime | Delayed | Overdue | Pending | NotMeasured.</summary>
    public string Performance { get; set; } = string.Empty;
    public int? AllottedTatMinutes { get; set; }
    public int? TatUsedMinutes { get; set; }
    /// <summary>Allotted − Used (negative = over).</summary>
    public int? TatDifferenceMinutes { get; set; }
}
