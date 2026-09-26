namespace Jarvis5.Dtos.EaFms;

// ============================================================================================
// EM Employee Report — per-employee, per-week view across every EA FMS module.
//
// A "work item" is one unit of work: a Delegation or Approval phase (Actual / Review / Rework,
// all counted for the doer), a Meeting (per doer), a Follow-up, or a Travel request.
// A work item belongs to the ISO week of its planned date (India time), Monday–Saturday.
//
// Everything is judged on TAT only (allotted vs used; used = elapsed minus pauses):
//   Pending    – not started (the TAT clock has not begun)
//   InProgress – started, used <= allotted
//   Overdue    – started, still open, used > allotted
//   OnTime     – completed, used <= allotted
//   Delayed    – completed, used > allotted
//   NoTat      – started/completed but no TAT allotted (shown, not judged)
// ============================================================================================

/// <summary>
/// Who and which week. By default the report is for the logged-in EA (from the login token).
/// EmployeeId/EmployeeName override that; Team=true gives the whole-team view.
/// </summary>
public class EmEmployeeReportQueryDto
{
    /// <summary>true: whole-team view (ignores the login and the employee filters).</summary>
    public bool Team { get; set; }
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
    /// <summary>Pending | InProgress | Overdue | OnTime | Delayed | NoTat.</summary>
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

/// <summary>One matrix column, judged on TAT only. Cancelled work is not counted as planned.</summary>
public class EmKpiCellDto
{
    /// <summary>Work planned in the week (the "Total Task" / "Current Week Planned").</summary>
    public int Planned { get; set; }
    /// <summary>Of those, completed ("Current Week Actual").</summary>
    public int Completed { get; set; }
    public int NotCompleted { get; set; }
    /// <summary>NotCompleted / Planned × 100.</summary>
    public decimal NotCompletedPct { get; set; }
    /// <summary>Completed with TAT used &lt;= allotted.</summary>
    public int OnTime { get; set; }
    /// <summary>Completed with TAT used &gt; allotted.</summary>
    public int Delayed { get; set; }
    /// <summary>Delayed / Completed × 100.</summary>
    public decimal DelayedPct { get; set; }
    /// <summary>Started, still open, within TAT.</summary>
    public int InProgress { get; set; }
    /// <summary>Started, still open, already over TAT.</summary>
    public int Overdue { get; set; }
    /// <summary>Not started yet.</summary>
    public int Pending { get; set; }
    /// <summary>Started/completed work with no TAT allotted.</summary>
    public int NoTat { get; set; }
    /// <summary>TAT allotted over the column's work that has a TAT.</summary>
    public int AllottedMinutes { get; set; }
    /// <summary>TAT used (elapsed minus pauses) over the same work.</summary>
    public int UsedMinutes { get; set; }
    /// <summary>Allotted − Used (negative = over).</summary>
    public int DifferenceMinutes { get; set; }
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

/// <summary>Task status donut (TAT-based). Rework items are counted only under Rework.</summary>
public class EmTaskStatusDto
{
    public int Total { get; set; }
    public int OnTime { get; set; }
    public int Delayed { get; set; }
    public int InProgress { get; set; }
    public int Overdue { get; set; }
    public int Pending { get; set; }
    public int Rework { get; set; }
    public int NoTat { get; set; }
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
    public int Overdue { get; set; }
}

// ---------------------------------------------------------------- Work items

/// <summary>One unit of work from any module, in one shape.</summary>
public class EmWorkItemDto
{
    /// <summary>Stable key, e.g. "Delegation:12:Review:1".</summary>
    public string ItemKey { get; set; } = string.Empty;
    /// <summary>How the EA is involved: DelegatedByMe | DelegatedToMe | Self | Organizer | Doer | Attendee | Owner | FollowedUp | Raised | Approver (empty in the team view).</summary>
    public string Relation { get; set; } = string.Empty;
    /// <summary>Delegation type / approval request type / meeting type / follow-up type / travel type.</summary>
    public string? Category { get; set; }
    /// <summary>"To whom": the doer of a delegation she assigned, who assigned it to her, who a follow-up waits on, the approver.</summary>
    public string? Counterparty { get; set; }
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
    /// <summary>Business due date, when the module has one (informational; performance is TAT-based).</summary>
    public DateTime? DueDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>NotStarted | InProgress | Paused | Completed | Cancelled.</summary>
    public string Status { get; set; } = string.Empty;
    public bool IsPaused { get; set; }
    /// <summary>Pending | InProgress | Overdue | OnTime | Delayed | NoTat (Cancelled for cancelled work).</summary>
    public string Performance { get; set; } = string.Empty;
    public int? AllottedTatMinutes { get; set; }
    /// <summary>Elapsed minus pauses; set for every started item, even without an allotted TAT.</summary>
    public int? TatUsedMinutes { get; set; }
    /// <summary>Allotted − Used (negative = over).</summary>
    public int? TatDifferenceMinutes { get; set; }
    /// <summary>Pauses inside this item's working window.</summary>
    public int PauseCount { get; set; }
    public int PausedMinutes { get; set; }
    /// <summary>EaTask classification (falls back to Category) and its subtype.</summary>
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    /// <summary>Who raised / assigned the work (delegation assigner, approval requester, meeting organiser, follow-up / travel creator).</summary>
    public string? AssignedByName { get; set; }
    /// <summary>Delegation assignee when set, otherwise the person who raised the work.</summary>
    public string? AssigneeName { get; set; }
    /// <summary>Who executes the work (delegation doer, approver, meeting doers, follow-up doer, travel creator).</summary>
    public string? DoerName { get; set; }
    public string? Priority { get; set; }
    public string? Description { get; set; }
    /// <summary>Pauses inside this item's working window.</summary>
    public List<EmItemPauseDto> Pauses { get; set; } = new();
    /// <summary>Files uploaded against the item's record (only filled for work-item and section item lists).</summary>
    public List<EmItemDocumentDto> Documents { get; set; } = new();
}

public class EmItemPauseDto
{
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int Minutes { get; set; }
    public string? Reason { get; set; }
}

public class EmItemDocumentDto
{
    public long AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

// ---------------------------------------------------------------- Sections (the EA's whole tracking)

public class EmSectionsQueryDto : EmEmployeeReportQueryDto
{
    /// <summary>true: every week (cumulative), ignoring Year/Week.</summary>
    public bool AllWeeks { get; set; }
    /// <summary>Maximum rows per section list (1–1000, default 200). Counts always cover everything.</summary>
    public int MaxRows { get; set; } = 200;
}

public class EmSectionsResponseDto
{
    public string? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public bool IsTeamView { get; set; }
    public bool AllWeeks { get; set; }
    public int? Year { get; set; }
    public int? Week { get; set; }
    public DateTime? WeekStart { get; set; }
    public DateTime? WeekEnd { get; set; }

    /// <summary>Cumulative: every section added together.</summary>
    public EmSummarySectionDto Summary { get; set; } = new();
    public EmDelegationSectionDto Delegations { get; set; } = new();
    public EmMeetingSectionDto Meetings { get; set; } = new();
    public EmFollowupSectionDto Followups { get; set; } = new();
    public EmApprovalSectionDto Approvals { get; set; } = new();
    public EmTravelSectionDto Travel { get; set; } = new();
    public EmDocumentSectionDto Documents { get; set; } = new();
    public EmPauseSectionDto Pauses { get; set; } = new();
}

/// <summary>A labelled count (e.g. a status or a recipient).</summary>
public class EmCountDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>A labelled KPI cell (e.g. one delegation type).</summary>
public class EmBreakdownDto
{
    public string Key { get; set; } = string.Empty;
    public EmKpiCellDto Kpi { get; set; } = new();
}

public class EmSummarySectionDto
{
    public EmKpiCellDto Kpi { get; set; } = new();
    /// <summary>The same KPIs per module.</summary>
    public List<EmBreakdownDto> ByModule { get; set; } = new();
    /// <summary>Actual / Review / Rework / Meeting.</summary>
    public List<EmBreakdownDto> ByTaskType { get; set; } = new();
    public int PauseCount { get; set; }
    public int PausedMinutes { get; set; }
    public int DocumentCount { get; set; }
}

public class EmDelegationSectionDto
{
    public EmKpiCellDto Kpi { get; set; } = new();
    /// <summary>Delegations the EA assigned to others (every phase done by the doer).</summary>
    public EmKpiCellDto DelegatedByMe { get; set; } = new();
    /// <summary>Delegations assigned to the EA.</summary>
    public EmKpiCellDto DelegatedToMe { get; set; } = new();
    public List<EmBreakdownDto> ByType { get; set; } = new();
    /// <summary>For delegations she assigned: one row per doer.</summary>
    public List<EmBreakdownDto> ByDoer { get; set; } = new();
    public List<EmBreakdownDto> ByPhase { get; set; } = new();
    public int DelegationCount { get; set; }
    public int ReviewCycles { get; set; }
    public int TotalRows { get; set; }
    public List<EmWorkItemDto> Items { get; set; } = new();
}

public class EmMeetingSectionDto
{
    public EmKpiCellDto Kpi { get; set; } = new();
    public int Organized { get; set; }
    public int AsDoer { get; set; }
    /// <summary>Meetings the EA is marked as having attended.</summary>
    public int Attended { get; set; }
    public List<EmCountDto> AttendanceByStatus { get; set; } = new();
    public List<EmBreakdownDto> ByType { get; set; } = new();
    public int ActionItems { get; set; }
    public int ActionItemsDelegated { get; set; }
    public int TotalRows { get; set; }
    public List<EmMeetingRowDto> Items { get; set; } = new();
}

public class EmMeetingRowDto : EmWorkItemDto
{
    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? MeetingMode { get; set; }
    public string? Location { get; set; }
    public string? OrganizerName { get; set; }
    public string? AttendanceStatus { get; set; }
    public DateTime? AttendedAt { get; set; }
    public int ActionItemCount { get; set; }
    public int DelegatedActionCount { get; set; }
    public string? DelegationDecision { get; set; }
}

public class EmFollowupSectionDto
{
    public EmKpiCellDto Kpi { get; set; } = new();
    public List<EmBreakdownDto> ByType { get; set; } = new();
    /// <summary>Follow-up attempts recorded (each with date and time).</summary>
    public int Attempts { get; set; }
    public int RemindersSent { get; set; }
    public List<EmCountDto> RemindersByChannel { get; set; } = new();
    public int Escalations { get; set; }
    public int OpenEscalations { get; set; }
    /// <summary>"To whom": follow-ups per person waited on.</summary>
    public List<EmCountDto> ByRecipient { get; set; } = new();
    public int TotalRows { get; set; }
    public List<EmFollowupRowDto> Items { get; set; } = new();
}

public class EmFollowupRowDto : EmWorkItemDto
{
    public string? WaitingOn { get; set; }
    public string? ReminderRecipientName { get; set; }
    public string? ReminderRecipientEmail { get; set; }
    public DateTime? ReminderAt { get; set; }
    public DateTime? LastFollowupAt { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public string? OutcomeCode { get; set; }
    public int OpenEscalations { get; set; }
    /// <summary>Every attempt, oldest first: when (date + time), by whom, what was noted.</summary>
    public List<EmFollowupAttemptDto> AttemptLog { get; set; } = new();
    /// <summary>Every reminder sent: channel, to whom, when.</summary>
    public List<EmReminderDto> ReminderLog { get; set; } = new();
}

public class EmFollowupAttemptDto
{
    public int SequenceNumber { get; set; }
    public DateTime FollowedUpAt { get; set; }
    public string? ById { get; set; }
    public string? ByName { get; set; }
    public string? Note { get; set; }
    public string? OutcomeCode { get; set; }
    public DateTime? NextFollowupAt { get; set; }
}

public class EmReminderDto
{
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public DateTime SentAt { get; set; }
    public string? SentByName { get; set; }
}

public class EmApprovalSectionDto
{
    public EmKpiCellDto Kpi { get; set; } = new();
    public int RequestCount { get; set; }
    public List<EmCountDto> ByStatus { get; set; } = new();
    public List<EmBreakdownDto> ByType { get; set; } = new();
    public List<EmCountDto> ByApprover { get; set; } = new();
    public List<EmBreakdownDto> ByPhase { get; set; } = new();
    public int ReviewCycles { get; set; }
    public int TotalRows { get; set; }
    public List<EmWorkItemDto> Items { get; set; } = new();
}

public class EmTravelSectionDto
{
    public EmKpiCellDto Kpi { get; set; } = new();
    public List<EmBreakdownDto> ByType { get; set; } = new();
    public List<EmCountDto> ByState { get; set; } = new();
    public int TotalRows { get; set; }
    public List<EmWorkItemDto> Items { get; set; } = new();
}

public class EmDocumentSectionDto
{
    public int Total { get; set; }
    public List<EmCountDto> ByModule { get; set; } = new();
    public int TotalRows { get; set; }
    public List<EmDocumentRowDto> Items { get; set; } = new();
}

public class EmDocumentRowDto
{
    public long AttachmentId { get; set; }
    public string? Module { get; set; }
    public string? RelatedEntity { get; set; }
    public string? RelatedEntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class EmPauseSectionDto
{
    public int Count { get; set; }
    public int TotalMinutes { get; set; }
    /// <summary>Pauses still open right now.</summary>
    public int OpenNow { get; set; }
    public List<EmCountDto> ByModule { get; set; } = new();
    public List<EmCountDto> TopReasons { get; set; } = new();
    public int TotalRows { get; set; }
    public List<EmPauseRowDto> Items { get; set; } = new();
}

public class EmPauseRowDto
{
    public string ItemKey { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int Minutes { get; set; }
    public string? Reason { get; set; }
}

