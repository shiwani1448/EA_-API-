using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public interface IEmEmployeeReportService
{
    Task<EmKpiResponseDto> GetKpisAsync(EmEmployeeReportQueryDto query, CancellationToken ct);
    Task<EmTrendResponseDto> GetTrendsAsync(EmEmployeeTrendQueryDto query, CancellationToken ct);
    Task<PagedResult<EmWorkItemDto>> GetWorkItemsAsync(EmWorkItemQueryDto query, CancellationToken ct);
    Task<EmSectionsResponseDto> GetSectionsAsync(EmSectionsQueryDto query, CancellationToken ct);
}

/// <summary>
/// Read-only EM report for one EA — by default the logged-in EA — covering everything she handled:
/// delegations she assigned or was assigned (every Actual / Review / Rework phase), meetings she
/// organised, is a doer of or attended, follow-ups she owns, recorded or does, approvals and travel
/// she raised (or approves), documents she uploaded and every pause. Each unit of work is a work
/// item placed in the ISO week of its planned date (India time, Monday–Saturday) and judged on TAT
/// only (allotted vs used, used = elapsed minus pauses): Pending = not started, InProgress = open
/// within TAT, Overdue = open over TAT, OnTime/Delayed = completed within/over TAT, NoTat = started
/// or completed with no TAT allotted. A person is matched against the stored id or display name
/// (several modules store only a name). Nothing is written.
/// </summary>
public sealed class EmEmployeeReportService(EaFmsDbContext db, ICurrentUserService currentUser) : IEmEmployeeReportService
{
    public const string ModDelegation = "Delegation", ModApproval = "Approval", ModMeeting = "Meeting",
        ModFollowup = "Follow-up", ModTravel = "Travel";
    private const string TypeActual = "Actual", TypeReview = "Review", TypeRework = "Rework", TypeMeeting = "Meeting";
    private const string StNotStarted = "NotStarted", StInProgress = "InProgress", StPaused = "Paused",
        StCompleted = "Completed", StCancelled = "Cancelled";
    private const string PerfOnTime = "OnTime", PerfDelayed = "Delayed", PerfOverdue = "Overdue",
        PerfPending = "Pending", PerfInProgress = "InProgress", PerfNoTat = "NoTat", PerfCancelled = "Cancelled";
    private const string RelDelegatedByMe = "DelegatedByMe", RelDelegatedToMe = "DelegatedToMe", RelSelf = "Self",
        RelOrganizer = "Organizer", RelDoer = "Doer", RelAttendee = "Attendee", RelOwner = "Owner",
        RelFollowedUp = "FollowedUp", RelRaised = "Raised", RelApprover = "Approver";
    private static readonly string[] Modules = [ModDelegation, ModApproval, ModMeeting, ModFollowup, ModTravel];
    private static readonly string[] TaskTypes = [TypeActual, TypeReview, TypeRework, TypeMeeting];

    // =================================================================================== public API

    public async Task<EmKpiResponseDto> GetKpisAsync(EmEmployeeReportQueryDto query, CancellationToken ct)
    {
        var who = Resolve(query);
        var (year, week) = ResolveWeek(query.Year, query.Week);
        var (start, end) = WeekRange(year, week);
        var (prevYear, prevWeek) = Shift(year, week, -1);
        var items = (await LoadAsync(who, ct)).Items;

        var current = items.Where(i => i.Year == year && i.Week == week && i.Status != StCancelled).ToList();
        var previous = items.Where(i => i.Year == prevYear && i.Week == prevWeek && i.Status != StCancelled).ToList();

        EmKpiCellDto Cell(Func<WorkItem, bool> pick)
        {
            var cell = BuildCell(current.Where(pick));
            var prev = BuildCell(previous.Where(pick));
            cell.Delta = new EmKpiDeltaDto
            {
                Planned = cell.Planned - prev.Planned, Completed = cell.Completed - prev.Completed,
                OnTime = cell.OnTime - prev.OnTime, Delayed = cell.Delayed - prev.Delayed
            };
            return cell;
        }

        var response = new EmKpiResponseDto
        {
            EmployeeId = who.Id, EmployeeName = who.DisplayName,
            Year = year, Week = week, WeekStart = start, WeekEnd = end,
            Matrix = new EmKpiMatrixDto
            {
                Summary = Cell(_ => true),
                Actual = Cell(i => i.TaskType == TypeActual),
                Review = Cell(i => i.TaskType == TypeReview),
                Rework = Cell(i => i.TaskType == TypeRework),
                Meeting = Cell(i => i.TaskType == TypeMeeting),
            },
            Modules = Modules.Select(m => new EmKpiModuleDto { Module = m, Kpi = Cell(i => i.Module == m) }).ToList(),
            TaskStatus = BuildTaskStatus(current),
            Tat = BuildTat(current),
            CarryForwardOverdue = items.Count(i => i.Performance == PerfOverdue && i.PlannedIndiaDate.HasValue && i.PlannedIndiaDate.Value < start),
        };
        response.FocusAreas = BuildFocusAreas(response);
        return response;
    }

    public async Task<EmTrendResponseDto> GetTrendsAsync(EmEmployeeTrendQueryDto query, CancellationToken ct)
    {
        if (query.Weeks is < 1 or > 12) throw new BadRequestException("Weeks must be between 1 and 12.");
        var who = Resolve(query);
        var (year, week) = ResolveWeek(query.Year, query.Week);
        var items = (await LoadAsync(who, ct)).Items;
        var response = new EmTrendResponseDto { EmployeeId = who.Id, EmployeeName = who.DisplayName };
        for (var offset = query.Weeks - 1; offset >= 0; offset--)
        {
            var (y, w) = Shift(year, week, -offset);
            var (start, end) = WeekRange(y, w);
            var inWeek = items.Where(i => i.Year == y && i.Week == w && i.Status != StCancelled).ToList();
            response.Weeks.Add(new EmTrendWeekDto
            {
                Year = y, Week = w, Label = $"W{w}", WeekStart = start, WeekEnd = end,
                Summary = Point(inWeek),
                Actual = Point(inWeek.Where(i => i.TaskType == TypeActual)),
                Review = Point(inWeek.Where(i => i.TaskType == TypeReview)),
                Rework = Point(inWeek.Where(i => i.TaskType == TypeRework)),
                Meeting = Point(inWeek.Where(i => i.TaskType == TypeMeeting)),
            });
        }
        return response;
    }

    public async Task<PagedResult<EmWorkItemDto>> GetWorkItemsAsync(EmWorkItemQueryDto query, CancellationToken ct)
    {
        if (query.Page < 1) throw new BadRequestException("Page must be 1 or greater.");
        if (query.PageSize is < 1 or > 200) throw new BadRequestException("PageSize must be between 1 and 200.");
        var module = Allowed(query.Module, Modules, "Module");
        var taskType = Allowed(query.TaskType, TaskTypes, "TaskType");
        var status = Allowed(query.Status, [StNotStarted, StInProgress, StPaused, StCompleted, StCancelled], "Status");
        var performance = Allowed(query.Performance, [PerfPending, PerfInProgress, PerfOverdue, PerfOnTime, PerfDelayed, PerfNoTat], "Performance");
        var who = Resolve(query);

        var allItems = (await LoadAsync(who, ct)).Items;
        IEnumerable<WorkItem> items = allItems;
        if (!query.AllWeeks)
        {
            var (year, week) = ResolveWeek(query.Year, query.Week);
            items = items.Where(i => i.Year == year && i.Week == week);
        }
        if (module is not null) items = items.Where(i => i.Module == module);
        if (taskType is not null) items = items.Where(i => i.TaskType == taskType);
        if (status is not null) items = items.Where(i => i.Status == status);
        if (performance is not null) items = items.Where(i => i.Performance == performance);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            items = items.Where(i => Contains(i.Title, term) || Contains(i.ReferenceNo, term) || Contains(i.OwnerName, term) || Contains(i.Counterparty, term)
                || Contains(i.DoerName, term) || Contains(i.AssigneeName, term));
        }

        var ordered = Newest(items).ToList();
        var page = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(i => Fill(new EmWorkItemDto(), i)).ToList();
        await AttachDocumentsAsync(page, allItems, ct);
        return new PagedResult<EmWorkItemDto>
        {
            PageNumber = query.Page, PageSize = query.PageSize, TotalCount = ordered.Count,
            Items = page,
        };
    }

    public async Task<EmSectionsResponseDto> GetSectionsAsync(EmSectionsQueryDto query, CancellationToken ct)
    {
        if (query.MaxRows is < 1 or > 1000) throw new BadRequestException("MaxRows must be between 1 and 1000.");
        var who = Resolve(query);
        var loaded = await LoadAsync(who, ct);
        var response = new EmSectionsResponseDto
        {
            EmployeeId = who.Id, EmployeeName = who.DisplayName, IsTeamView = who.IsTeam, AllWeeks = query.AllWeeks
        };

        Func<DateTime?, bool> inPeriod = _ => true;
        if (!query.AllWeeks)
        {
            var (year, week) = ResolveWeek(query.Year, query.Week);
            var (start, end) = WeekRange(year, week);
            (response.Year, response.Week, response.WeekStart, response.WeekEnd) = (year, week, start, end);
            inPeriod = d => d.HasValue && WeekOf(IndiaBusinessCalendar.ToIndiaDate(d.Value)) == (year, week);
        }
        var items = loaded.Items.Where(i => i.Status != StCancelled && (query.AllWeeks || inPeriod(i.PlannedDate))).ToList();
        var max = query.MaxRows;
        var now = Clock.UtcNowTz;

        // ---- Delegations
        var del = items.Where(i => i.Module == ModDelegation).ToList();
        var delRecords = del.Select(i => i.RecordId).Distinct().Select(id => loaded.Delegations[id]).ToList();
        response.Delegations = new EmDelegationSectionDto
        {
            Kpi = BuildCell(del),
            DelegatedByMe = BuildCell(del.Where(i => i.Relation is RelDelegatedByMe or RelSelf)),
            DelegatedToMe = BuildCell(del.Where(i => i.Relation is RelDelegatedToMe or RelSelf)),
            ByType = Breakdown(del, i => i.Category ?? "(No type)"),
            ByDoer = Breakdown(del.Where(i => i.Relation is RelDelegatedByMe or RelSelf), i => i.OwnerName ?? i.OwnerId ?? "(Unknown)"),
            ByPhase = Breakdown(del, i => i.TaskType),
            DelegationCount = delRecords.Count,
            ReviewCycles = del.Where(i => i.TaskType == TypeReview).Select(i => (i.RecordId, i.ReviewCycleNumber)).Distinct().Count(),
            TotalRows = del.Count,
            Items = Newest(del).Take(max).Select(i => Fill(new EmWorkItemDto(), i)).ToList(),
        };

        // ---- Meetings
        var meet = items.Where(i => i.Module == ModMeeting).ToList();
        var meetingIds = meet.Select(i => i.RecordId).ToHashSet();
        var myAttendance = meet.Select(i => loaded.MyAttendance.GetValueOrDefault(i.RecordId)).Where(a => a is not null).Select(a => a!).ToList();
        response.Meetings = new EmMeetingSectionDto
        {
            Kpi = BuildCell(meet),
            Organized = meet.Count(i => i.Relation == RelOrganizer),
            AsDoer = meet.Count(i => i.Relation == RelDoer || loaded.MeetingDoerOf.Contains(i.RecordId)),
            Attended = myAttendance.Count(IsAttended),
            AttendanceByStatus = Counts(myAttendance, a => a.AttendanceStatus ?? (a.AttendedAt.HasValue ? "Attended" : "(Not marked)")),
            ByType = Breakdown(meet, i => i.Category ?? "(No type)"),
            ActionItems = loaded.Actions.Where(a => meetingIds.Contains(a.MeetingId)).Count(),
            ActionItemsDelegated = loaded.Actions.Count(a => meetingIds.Contains(a.MeetingId) && loaded.DelegatedActionIds.Contains(a.Id)),
            TotalRows = meet.Count,
            Items = Newest(meet).Take(max).Select(i =>
            {
                var m = loaded.Meetings[i.RecordId];
                var att = loaded.MyAttendance.GetValueOrDefault(m.Id);
                var actions = loaded.Actions.Where(a => a.MeetingId == m.Id).ToList();
                var row = Fill(new EmMeetingRowDto(), i);
                row.StartDateTime = m.StartDateTime; row.EndDateTime = m.EndDateTime; row.MeetingMode = m.MeetingMode;
                row.Location = m.Location; row.OrganizerName = m.OrganizerName; row.AttendanceStatus = att?.AttendanceStatus;
                row.AttendedAt = att?.AttendedAt; row.ActionItemCount = actions.Count;
                row.DelegatedActionCount = actions.Count(a => loaded.DelegatedActionIds.Contains(a.Id));
                row.DelegationDecision = m.DelegationDecision;
                return row;
            }).ToList(),
        };

        // ---- Follow-ups
        var fol = items.Where(i => i.Module == ModFollowup).ToList();
        var folIds = fol.Select(i => i.RecordId).ToHashSet();
        var cycles = loaded.Cycles.Where(c => folIds.Contains(c.FollowupId)).ToList();
        var reminders = loaded.Reminders.Where(r => folIds.Contains(r.FollowupId)).ToList();
        var escalations = loaded.Escalations.Where(e => e.FollowupId.HasValue && folIds.Contains(e.FollowupId.Value)).ToList();
        response.Followups = new EmFollowupSectionDto
        {
            Kpi = BuildCell(fol),
            ByType = Breakdown(fol, i => i.Category ?? "(No type)"),
            Attempts = cycles.Count,
            RemindersSent = reminders.Count,
            RemindersByChannel = Counts(reminders, r => r.Channel),
            Escalations = escalations.Count,
            OpenEscalations = escalations.Count(e => e.ResolvedAt is null),
            ByRecipient = Counts(fol, i => i.Counterparty ?? "(Not set)"),
            TotalRows = fol.Count,
            Items = Newest(fol).Take(max).Select(i =>
            {
                var f = loaded.Followups[i.RecordId];
                var row = Fill(new EmFollowupRowDto(), i);
                row.WaitingOn = f.WaitingOnName ?? f.WaitingOnExternal; row.ReminderRecipientName = f.ReminderRecipientName;
                row.ReminderRecipientEmail = f.ReminderRecipientEmail; row.ReminderAt = f.ReminderAt; row.LastFollowupAt = f.LastFollowupAt;
                row.NextFollowupAt = f.NextFollowupAt; row.OutcomeCode = f.OutcomeCode;
                row.OpenEscalations = escalations.Count(e => e.FollowupId == f.Id && e.ResolvedAt is null);
                row.AttemptLog = cycles.Where(c => c.FollowupId == f.Id).OrderBy(c => c.FollowedUpAt).ThenBy(c => c.Id).Select(c => new EmFollowupAttemptDto
                {
                    SequenceNumber = c.SequenceNumber, FollowedUpAt = c.FollowedUpAt, ById = c.FollowedUpByEmployeeId,
                    ByName = c.FollowedUpByEmployeeName ?? c.CreatedBy, Note = c.Note, OutcomeCode = c.OutcomeCode, NextFollowupAt = c.NextFollowupAt
                }).ToList();
                row.ReminderLog = reminders.Where(r => r.FollowupId == f.Id).OrderBy(r => r.SentAt).Select(r => new EmReminderDto
                {
                    Channel = r.Channel, Recipient = r.Recipient, RecipientName = r.RecipientName, SentAt = r.SentAt, SentByName = r.SentByName
                }).ToList();
                return row;
            }).ToList(),
        };

        // ---- Approvals
        var appr = items.Where(i => i.Module == ModApproval).ToList();
        var apprRecords = appr.Select(i => i.RecordId).Distinct().Select(id => loaded.Approvals[id]).ToList();
        response.Approvals = new EmApprovalSectionDto
        {
            Kpi = BuildCell(appr),
            RequestCount = apprRecords.Count,
            ByStatus = Counts(apprRecords, a => a.WorkflowStatus ?? "Draft"),
            ByType = Breakdown(appr, i => i.Category ?? "(No type)"),
            ByApprover = Counts(apprRecords, a => a.ApproverName ?? a.ApproverId ?? "(Not set)"),
            ByPhase = Breakdown(appr, i => i.TaskType),
            ReviewCycles = appr.Where(i => i.TaskType == TypeReview).Select(i => (i.RecordId, i.ReviewCycleNumber)).Distinct().Count(),
            TotalRows = appr.Count,
            Items = Newest(appr).Take(max).Select(i => Fill(new EmWorkItemDto(), i)).ToList(),
        };

        // ---- Travel
        var trv = items.Where(i => i.Module == ModTravel).ToList();
        response.Travel = new EmTravelSectionDto
        {
            Kpi = BuildCell(trv),
            ByType = Breakdown(trv, i => i.Category ?? "(No type)"),
            ByState = Counts(trv, i => loaded.Travel[i.RecordId].BusinessState),
            TotalRows = trv.Count,
            Items = Newest(trv).Take(max).Select(i => Fill(new EmWorkItemDto(), i)).ToList(),
        };
        await AttachDocumentsAsync(response.Delegations.Items.Concat(response.Approvals.Items).Concat(response.Travel.Items).ToList(), loaded.Items, ct);

        // ---- Documents (by upload time)
        var docs = loaded.Documents.Where(d => query.AllWeeks || inPeriod(d.UploadedAt)).OrderByDescending(d => d.UploadedAt).ThenByDescending(d => d.Id).ToList();
        response.Documents = new EmDocumentSectionDto
        {
            Total = docs.Count,
            ByModule = Counts(docs, d => d.RelatedModule ?? "(Unknown)"),
            TotalRows = docs.Count,
            Items = docs.Take(max).Select(d => new EmDocumentRowDto
            {
                AttachmentId = d.Id, Module = d.RelatedModule, RelatedEntity = d.RelatedEntity, RelatedEntityId = d.RelatedEntityId,
                FileName = d.OriginalFileName, Size = d.Size, UploadedBy = d.UploadedBy, UploadedAt = d.UploadedAt
            }).ToList(),
        };

        // ---- Pauses (inside the working window of the period's work items)
        var pauses = items.SelectMany(i => i.PauseList.Select(p => (Item: i, Pause: p))).ToList();
        int Minutes(WorkPause p) => (int)((p.EndAt ?? now) - p.StartAt).TotalMinutes;
        response.Pauses = new EmPauseSectionDto
        {
            Count = pauses.Count,
            TotalMinutes = pauses.Sum(x => Minutes(x.Pause)),
            OpenNow = pauses.Count(x => x.Pause.EndAt is null),
            ByModule = Counts(pauses, x => x.Item.Module),
            TopReasons = Counts(pauses, x => string.IsNullOrWhiteSpace(x.Pause.Reason) ? "(No reason)" : x.Pause.Reason!.Trim()).Take(10).ToList(),
            TotalRows = pauses.Count,
            Items = pauses.OrderByDescending(x => x.Pause.StartAt).Take(max).Select(x => new EmPauseRowDto
            {
                ItemKey = x.Item.ItemKey, Module = x.Item.Module, TaskType = x.Item.TaskType, Title = x.Item.Title,
                StartAt = x.Pause.StartAt, EndAt = x.Pause.EndAt, Minutes = Minutes(x.Pause), Reason = x.Pause.Reason
            }).ToList(),
        };

        // ---- Cumulative summary
        response.Summary = new EmSummarySectionDto
        {
            Kpi = BuildCell(items),
            ByModule = Modules.Select(m => new EmBreakdownDto { Key = m, Kpi = BuildCell(items.Where(i => i.Module == m)) }).ToList(),
            ByTaskType = TaskTypes.Select(t => new EmBreakdownDto { Key = t, Kpi = BuildCell(items.Where(i => i.TaskType == t)) }).ToList(),
            PauseCount = response.Pauses.Count,
            PausedMinutes = response.Pauses.TotalMinutes,
            DocumentCount = response.Documents.Total,
        };
        return response;
    }

    // =================================================================================== identity

    /// <summary>The person the report is for. IsTeam = everyone (no matching).</summary>
    private sealed record Who(string? Id, string? Name, string? DisplayName, bool IsTeam)
    {
        /// <summary>True when any stored value (an id or a display name) identifies this person.</summary>
        public bool Is(params string?[] stored)
        {
            if (IsTeam) return true;
            foreach (var value in stored)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;
                if (Id is not null && string.Equals(value.Trim(), Id, StringComparison.OrdinalIgnoreCase)) return true;
                if (Name is not null && NormName(value) == Name) return true;
            }
            return false;
        }
    }

    /// <summary>Team flag → everyone; explicit employee filters → that person; otherwise the logged-in EA; no login → everyone.</summary>
    private Who Resolve(EmEmployeeReportQueryDto query)
    {
        if (query.Team) return new Who(null, null, null, true);
        var id = Clean(query.EmployeeId);
        var name = Clean(query.EmployeeName);
        if (id is null && name is null)
        {
            id = currentUser.ActorId();
            name = currentUser.ActorName();
        }
        return id is null && name is null ? new Who(null, null, null, true) : new Who(id, NormName(name), name, false);
    }

    // =================================================================================== loading

    private sealed class WorkItem
    {
        public string ItemKey = "", Module = "", TaskType = "", Status = "", Performance = "", Relation = "";
        public int ReviewCycleNumber, PauseCount, PausedMinutes;
        public long RecordId;
        public long? EaTaskId;
        public string? ReferenceNo, Title, OwnerId, OwnerName, Category, Counterparty;
        public string? Type, Subtype, AssignedByName, AssigneeName, DoerName, Priority, Description;
        public DateTime? PlannedDate, PlannedIndiaDate, DueDate, StartedAt, CompletedAt;
        public int? Year, Week, AllottedTatMinutes, TatUsedMinutes;
        public bool IsPaused;
        public List<WorkPause> PauseList = [];
    }

    private sealed class Loaded
    {
        public List<WorkItem> Items = [];
        public Dictionary<long, Entities.EaFms.Delegation> Delegations = [];
        public Dictionary<long, ApprovalRequest> Approvals = [];
        public Dictionary<long, Meeting> Meetings = [];
        public Dictionary<long, Followup> Followups = [];
        public Dictionary<long, TravelRequest> Travel = [];
        public Dictionary<long, MeetingAttendee> MyAttendance = [];
        public HashSet<long> MeetingDoerOf = [];
        public List<MeetingAction> Actions = [];
        public HashSet<long> DelegatedActionIds = [];
        public List<FollowupCycle> Cycles = [];
        public List<FollowupReminderLog> Reminders = [];
        public List<Escalation> Escalations = [];
        public List<Attachment> Documents = [];
    }

    /// <summary>Loads and normalises every module's work for the person (all weeks).</summary>
    private async Task<Loaded> LoadAsync(Who who, CancellationToken ct)
    {
        var now = Clock.UtcNowTz;
        var today = IndiaBusinessCalendar.ToIndiaDate(now);
        var loaded = new Loaded();

        // ---- source rows; matching is in memory so id and name rules are identical everywhere
        var delegations = (await db.Delegations.AsNoTracking().Where(d => !d.IsDeleted).ToListAsync(ct))
            .Where(d => who.Is(d.AssignedById, d.AssignedByNameSnapshot) || who.Is(d.DoerId, d.DoerNameSnapshot)).ToList();
        var approvals = (await db.ApprovalRequests.AsNoTracking().Where(a => !a.IsDeleted).ToListAsync(ct))
            .Where(a => who.Is(a.RequestedBy, a.CreatedBy) || who.Is(a.ApproverId, a.ApproverName)).ToList();
        var attendees = await db.MeetingAttendees.AsNoTracking().Where(a => !a.IsDeleted).ToListAsync(ct);
        var myAttendance = who.IsTeam ? new Dictionary<long, MeetingAttendee>()
            : attendees.Where(a => who.Is(a.ParticipantId, a.ParticipantName)).GroupBy(a => a.MeetingId).ToDictionary(g => g.Key, g => g.First());
        var meetings = (await db.Meetings.AsNoTracking().Where(m => !m.IsDeleted).ToListAsync(ct))
            .Where(m => who.Is(m.CreatedBy, m.OrganizerId, m.OrganizerName) || IsMeetingDoer(who, m) || myAttendance.ContainsKey(m.Id)).ToList();
        var allFollowups = await db.Followups.AsNoTracking().Where(f => !f.IsDeleted && f.EaTaskId != null).ToListAsync(ct);
        var allCycles = await db.FollowupCycles.AsNoTracking().ToListAsync(ct);
        var followedUpByMe = allCycles.Where(c => who.Is(c.FollowedUpByEmployeeId, c.FollowedUpByEmployeeName)).Select(c => c.FollowupId).ToHashSet();
        var followups = allFollowups.Where(f => who.Is(f.CreatedByEmployeeId, f.CreatedByEmployeeName, f.CreatedBy)
            || who.Is(f.DoerId, f.DoerName) || followedUpByMe.Contains(f.Id)).ToList();
        var travel = (await db.TravelRequests.AsNoTracking().Where(t => !t.IsDeleted).ToListAsync(ct))
            .Where(t => who.Is(t.CreatedBy) || who.Is(t.ApproverId, t.ApproverNameSnapshot)).ToList();

        loaded.Delegations = delegations.ToDictionary(d => d.Id);
        loaded.Approvals = approvals.ToDictionary(a => a.Id);
        loaded.Meetings = meetings.ToDictionary(m => m.Id);
        loaded.Followups = followups.ToDictionary(f => f.Id);
        loaded.Travel = travel.ToDictionary(t => t.Id);
        loaded.MyAttendance = myAttendance;
        loaded.MeetingDoerOf = who.IsTeam ? [] : meetings.Where(m => IsMeetingDoer(who, m)).Select(m => m.Id).ToHashSet();

        var followupIds = followups.Select(f => f.Id).ToHashSet();
        loaded.Cycles = allCycles.Where(c => followupIds.Contains(c.FollowupId)).ToList();
        loaded.Reminders = followupIds.Count == 0 ? [] : await db.FollowupReminderLogs.AsNoTracking().Where(r => followupIds.Contains(r.FollowupId)).ToListAsync(ct);
        loaded.Escalations = followupIds.Count == 0 ? [] : await db.Escalations.AsNoTracking()
            .Where(e => !e.IsDeleted && e.FollowupId.HasValue && followupIds.Contains(e.FollowupId.Value)).ToListAsync(ct);
        var meetingIds = meetings.Select(m => m.Id).ToList();
        loaded.Actions = meetingIds.Count == 0 ? [] : await db.MeetingActions.AsNoTracking().Where(a => !a.IsDeleted && meetingIds.Contains(a.MeetingId)).ToListAsync(ct);
        loaded.DelegatedActionIds = loaded.Actions.Count == 0 ? []
            : (await MeetingDelegationService.LoadDelegationIdsAsync(db, loaded.Actions.Select(a => a.Id), ct)).Keys.ToHashSet();
        loaded.Documents = (await db.Attachments.AsNoTracking().Where(a => a.IsActive && !a.IsDeleted).ToListAsync(ct))
            .Where(a => !who.IsTeam ? who.Is(a.UploadedBy, a.CreatedBy) : true).ToList();

        var delegationIds = delegations.Select(d => d.Id).ToList();
        var approvalIds = approvals.Select(a => a.Id).ToList();
        var delegationPhases = delegationIds.Count == 0 ? [] : await db.DelegationPhaseTats.AsNoTracking()
            .Where(p => delegationIds.Contains(p.DelegationId)).ToListAsync(ct);
        var approvalPhases = approvalIds.Count == 0 ? [] : await db.ApprovalPhaseTats.AsNoTracking()
            .Where(p => approvalIds.Contains(p.ApprovalRequestId)).ToListAsync(ct);

        var meetingIdTexts = meetings.Select(m => m.Id.ToString(CultureInfo.InvariantCulture)).ToList();
        var taskIds = delegations.Select(d => d.EaTaskId).Concat(approvals.Select(a => a.EaTaskId))
            .Concat(followups.Select(f => f.EaTaskId!.Value)).Concat(travel.Select(t => t.EaTaskId)).Distinct().ToList();
        var tasks = await db.Tasks.AsNoTracking()
            .Where(t => !t.IsDeleted && (taskIds.Contains(t.Id) || (t.ModuleName == ModMeeting && meetingIdTexts.Contains(t.BusinessRecordId))))
            .ToListAsync(ct);
        var taskById = tasks.ToDictionary(t => t.Id);
        var meetingTaskByRecord = tasks.Where(t => t.ModuleName == ModMeeting)
            .GroupBy(t => t.BusinessRecordId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Id).First());

        var workflowIds = tasks.Where(t => t.WorkflowInstanceId.HasValue).Select(t => t.WorkflowInstanceId!.Value)
            .Concat(meetings.Where(m => m.WorkflowInstanceId.HasValue).Select(m => m.WorkflowInstanceId!.Value)).Distinct().ToList();
        var pausesByWorkflow = workflowIds.Count == 0 ? new Dictionary<long, List<WorkPause>>()
            : (await db.WorkPauses.AsNoTracking().Where(p => p.WorkflowInstanceId.HasValue && workflowIds.Contains(p.WorkflowInstanceId.Value) && !p.IsDeleted)
                .ToListAsync(ct)).GroupBy(p => p.WorkflowInstanceId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        IReadOnlyCollection<WorkPause> Pauses(long? workflowId) =>
            workflowId.HasValue && pausesByWorkflow.TryGetValue(workflowId.Value, out var list) ? list : [];

        var items = loaded.Items;

        // Detail fields shared by every item added for one record since index `from` (all its phases).
        void Describe(int from, EaTask? task, string? category, string? assignedBy, string? assignee, string? doer, string? priority, string? description)
        {
            for (var k = from; k < items.Count; k++)
            {
                var it = items[k];
                it.Type = Clean(task?.Type) ?? it.Category ?? Clean(category);
                it.Subtype = Clean(task?.Subtype);
                it.AssignedByName = Clean(assignedBy);
                it.AssigneeName = Clean(assignee) ?? it.AssignedByName;
                it.DoerName = Clean(doer);
                it.Priority = Clean(priority);
                it.Description = Clean(description ?? task?.Description);
            }
        }

        // ---- Delegation: one item per phase; relation = assigned by me / to me / self
        var phasesByDelegation = delegationPhases.GroupBy(p => p.DelegationId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var d in delegations)
        {
            var from = items.Count;
            var byMe = !who.IsTeam && who.Is(d.AssignedById, d.AssignedByNameSnapshot);
            var toMe = !who.IsTeam && who.Is(d.DoerId, d.DoerNameSnapshot);
            var relation = who.IsTeam ? "" : byMe && toMe ? RelSelf : byMe ? RelDelegatedByMe : RelDelegatedToMe;
            var counterparty = relation == RelDelegatedByMe ? d.DoerNameSnapshot ?? d.DoerId
                : relation == RelDelegatedToMe ? d.AssignedByNameSnapshot ?? d.AssignedById : null;
            taskById.TryGetValue(d.EaTaskId, out var task);
            var pauses = Pauses(task?.WorkflowInstanceId);
            var phases = phasesByDelegation.GetValueOrDefault(d.Id) ?? [];
            if (phases.Count == 0)
            {
                // Not started yet (the Actual phase row is created on Start).
                var completed = d.Status == DelegationStatus.Completed;
                items.Add(NewItem(ModDelegation, TypeActual, 0, d.Id, d.EaTaskId, d.ReferenceNo, d.Title, d.DoerId, d.DoerNameSnapshot,
                    planned: d.DueDate ?? d.CreatedDate, due: d.DueDate, started: d.StartedAt, completedAt: completed ? d.CompletedAt : null,
                    status: completed ? StCompleted : StNotStarted, isPaused: false, allotted: task?.AllottedTatMinutes,
                    used: completed ? task?.TatUsedMinutes ?? Elapsed(d.StartedAt, d.CompletedAt, pauses) : null,
                    relation, d.DelegationType, counterparty, pauses, now, today));
                DescribeDelegation();
                continue;
            }
            foreach (var p in phases.OrderBy(p => p.ReviewCycleNumber).ThenBy(p => p.Id))
            {
                var (status, paused, used) = PhaseState(p.StartedAt, p.EndedAt, p.AllottedTatMinutes, p.TatUsedMinutes, pauses, now);
                var isActual = p.TaskType == TypeActual;
                items.Add(NewItem(ModDelegation, p.TaskType, p.ReviewCycleNumber, d.Id, d.EaTaskId, d.ReferenceNo, d.Title, d.DoerId, d.DoerNameSnapshot,
                    planned: isActual ? d.DueDate ?? p.StartedAt ?? p.CreatedDate : p.StartedAt ?? p.CreatedDate,
                    due: isActual ? d.DueDate : null, started: p.StartedAt, completedAt: p.EndedAt,
                    status, paused, p.AllottedTatMinutes, used, relation, d.DelegationType, counterparty, pauses, now, today));
            }
            DescribeDelegation();

            void DescribeDelegation() => Describe(from, task, d.DelegationType, d.AssignedByNameSnapshot ?? d.AssignedById,
                d.AssigneeNameSnapshot ?? d.AssigneeId, d.DoerNameSnapshot ?? d.DoerId, d.Priority, d.Description);
        }

        // ---- Approval: one item per phase; relation = raised by me / I approve
        var phasesByApproval = approvalPhases.GroupBy(p => p.ApprovalRequestId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var a in approvals)
        {
            var from = items.Count;
            var relation = who.IsTeam ? "" : who.Is(a.RequestedBy, a.CreatedBy) ? RelRaised : RelApprover;
            var counterparty = relation == RelApprover ? a.RequestedBy ?? a.CreatedBy : a.ApproverName ?? a.ApproverId;
            taskById.TryGetValue(a.EaTaskId, out var task);
            var pauses = Pauses(task?.WorkflowInstanceId);
            var ownerName = a.RequestedBy ?? a.CreatedBy;
            var rejected = string.Equals(a.WorkflowStatus, "Rejected", StringComparison.OrdinalIgnoreCase);
            var phases = phasesByApproval.GetValueOrDefault(a.Id) ?? [];
            if (phases.Count == 0)
            {
                var approved = string.Equals(a.WorkflowStatus, "Approved", StringComparison.OrdinalIgnoreCase);
                items.Add(NewItem(ModApproval, TypeActual, 0, a.Id, a.EaTaskId, a.ReferenceNo, a.RequestTitle, null, ownerName,
                    planned: a.RequiredApprovalDate ?? a.CreatedAt, due: a.RequiredApprovalDate, started: a.SubmittedAt,
                    completedAt: approved ? a.ApprovedAt : null, status: approved ? StCompleted : rejected ? StCancelled : StNotStarted,
                    isPaused: false, allotted: task?.AllottedTatMinutes, used: approved ? task?.TatUsedMinutes ?? Elapsed(a.SubmittedAt, a.ApprovedAt, pauses) : null,
                    relation, a.RequestType, counterparty, pauses, now, today));
                DescribeApproval();
                continue;
            }
            foreach (var p in phases.OrderBy(p => p.ReviewCycleNumber).ThenBy(p => p.Id))
            {
                var (status, paused, used) = PhaseState(p.StartedAt, p.EndedAt, p.AllottedTatMinutes, p.TatUsedMinutes, pauses, now);
                if (rejected && p.EndedAt is null) (status, paused) = (StCancelled, false);
                var isActual = p.TaskType == TypeActual;
                items.Add(NewItem(ModApproval, p.TaskType, p.ReviewCycleNumber, a.Id, a.EaTaskId, a.ReferenceNo, a.RequestTitle, null, ownerName,
                    planned: isActual ? a.RequiredApprovalDate ?? p.StartedAt ?? p.CreatedDate : p.StartedAt ?? p.CreatedDate,
                    due: isActual ? a.RequiredApprovalDate : null, started: p.StartedAt, completedAt: p.EndedAt,
                    status, paused, p.AllottedTatMinutes, used, relation, a.RequestType, counterparty, pauses, now, today));
            }
            DescribeApproval();

            void DescribeApproval() => Describe(from, task, a.RequestType, ownerName, null, a.ApproverName ?? a.ApproverId, a.Priority, a.Description);
        }

        // ---- Meeting: one item per meeting; relation = organizer / doer / attendee
        foreach (var m in meetings)
        {
            var relation = who.IsTeam ? "" : who.Is(m.CreatedBy, m.OrganizerId, m.OrganizerName) ? RelOrganizer
                : IsMeetingDoer(who, m) ? RelDoer : RelAttendee;
            meetingTaskByRecord.TryGetValue(m.Id.ToString(CultureInfo.InvariantCulture), out var task);
            var pauses = Pauses(task?.WorkflowInstanceId ?? m.WorkflowInstanceId);
            var (status, paused, used) = TaskState(task, pauses, now, fallbackCompletedAt: m.CompletedAt);
            var meetingDate = m.StartDateTime ?? m.MeetingDate;
            var doers = m.DoerNames.Length == 0 ? null : string.Join(", ", m.DoerNames);
            items.Add(NewItem(ModMeeting, TypeMeeting, 0, m.Id, task?.Id, m.MeetingNumber, m.Title, m.OrganizerId, m.OrganizerName ?? m.CreatedBy,
                planned: meetingDate ?? m.CreatedDate, due: meetingDate, started: task?.StartedAt, completedAt: task?.CompletedAt ?? m.CompletedAt,
                status, paused, task?.AllottedTatMinutes, used, relation, m.MeetingType, doers, pauses, now, today));
            Describe(items.Count - 1, task, m.MeetingType, m.OrganizerName ?? m.CreatedBy, null, doers, m.Priority, m.Description ?? m.Purpose);
        }

        // ---- Follow-up: its own Actual execution task; relation = owner / doer / followed up
        foreach (var f in followups)
        {
            var relation = who.IsTeam ? "" : who.Is(f.CreatedByEmployeeId, f.CreatedByEmployeeName, f.CreatedBy) ? RelOwner
                : who.Is(f.DoerId, f.DoerName) ? RelDoer : RelFollowedUp;
            taskById.TryGetValue(f.EaTaskId!.Value, out var task);
            var pauses = Pauses(task?.WorkflowInstanceId);
            var (status, paused, used) = TaskState(task, pauses, now, fallbackCompletedAt: f.CompletedAt);
            items.Add(NewItem(ModFollowup, TypeActual, 0, f.Id, f.EaTaskId, null, f.Subject, f.DoerId, f.DoerName,
                planned: f.DueAt, due: f.DueAt, started: task?.StartedAt, completedAt: task?.CompletedAt ?? f.CompletedAt,
                status, paused, task?.AllottedTatMinutes, used, relation, f.Type,
                f.WaitingOnName ?? f.WaitingOnExternal ?? f.ReminderRecipientName, pauses, now, today));
            Describe(items.Count - 1, task, f.Type, f.CreatedByEmployeeName ?? f.CreatedBy, null, f.DoerName ?? f.DoerId, null, null);
        }

        // ---- Travel: one item per request; relation = raised by me / I approve
        foreach (var t in travel)
        {
            var relation = who.IsTeam ? "" : who.Is(t.CreatedBy) ? RelRaised : RelApprover;
            taskById.TryGetValue(t.EaTaskId, out var task);
            var pauses = Pauses(task?.WorkflowInstanceId);
            var (status, paused, used) = TaskState(task, pauses, now, fallbackCompletedAt: t.CompletedAt);
            items.Add(NewItem(ModTravel, TypeActual, 0, t.Id, t.EaTaskId, t.ReferenceNo, t.Purpose ?? $"Travel {t.ReferenceNo}", null, t.CreatedBy,
                planned: t.RequiredDate ?? t.CreatedDate, due: t.RequiredDate, started: task?.StartedAt ?? t.StartedAt,
                completedAt: task?.CompletedAt ?? t.CompletedAt, status, paused, task?.AllottedTatMinutes, used,
                relation, t.TravelType, relation == RelApprover ? t.CreatedBy : t.ApproverNameSnapshot ?? t.ApproverId, pauses, now, today));
            Describe(items.Count - 1, task, t.TravelType, t.CreatedBy, null, t.CreatedBy, t.Priority, t.Purpose);
        }

        return loaded;
    }

    private static bool IsMeetingDoer(Who who, Meeting m)
    {
        if (who.IsTeam) return false;
        for (var i = 0; i < m.DoerIds.Length; i++)
            if (who.Is(m.DoerIds[i], i < m.DoerNames.Length ? m.DoerNames[i] : null)) return true;
        return m.DoerNames.Any(n => who.Is(n));
    }

    private static bool IsAttended(MeetingAttendee a) =>
        a.AttendedAt.HasValue || (a.AttendanceStatus?.Trim().ToLowerInvariant() is "attended" or "present");

    private static WorkItem NewItem(string module, string taskType, int cycle, long recordId, long? eaTaskId, string? reference, string? title,
        string? ownerId, string? ownerName, DateTime? planned, DateTime? due, DateTime? started, DateTime? completedAt,
        string status, bool isPaused, int? allotted, int? used, string relation, string? category, string? counterparty,
        IReadOnlyCollection<WorkPause> workflowPauses, DateTime now, DateTime today)
    {
        var plannedIndia = planned.HasValue ? IndiaBusinessCalendar.ToIndiaDate(planned.Value) : (DateTime?)null;
        var (year, week) = plannedIndia.HasValue ? WeekOf(plannedIndia.Value) : (null, null);
        var item = new WorkItem
        {
            ItemKey = $"{module}:{recordId}:{taskType}:{cycle}",
            Module = module, TaskType = taskType, ReviewCycleNumber = cycle, RecordId = recordId, EaTaskId = eaTaskId,
            ReferenceNo = reference, Title = title, OwnerId = ownerId, OwnerName = ownerName,
            Relation = relation, Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(), Counterparty = counterparty,
            PlannedDate = planned, PlannedIndiaDate = plannedIndia, Year = year, Week = week,
            DueDate = due, StartedAt = started, CompletedAt = status == StCompleted ? completedAt : null,
            Status = status, IsPaused = isPaused, AllottedTatMinutes = allotted, TatUsedMinutes = used,
        };
        // Pauses inside this item's own working window (a phase only owns the pauses during that phase).
        if (started.HasValue)
        {
            var windowEnd = item.CompletedAt ?? now;
            item.PauseList = workflowPauses.Where(p => WorkPauseClassifier.IsSimplePause(p) && p.StartAt >= started.Value && p.StartAt < windowEnd).ToList();
            item.PauseCount = item.PauseList.Count;
            item.PausedMinutes = (int)WorkPauseClassifier.GetPausedDuration(started.Value, windowEnd, item.PauseList).TotalMinutes;
        }
        item.Performance = Classify(item, today);
        return item;
    }

    /// <summary>Phase row state: frozen TAT used once ended (or elapsed minus pauses when nothing was frozen), live while open.</summary>
    private static (string Status, bool Paused, int? Used) PhaseState(DateTime? startedAt, DateTime? endedAt, int? allotted, int? frozenUsed,
        IReadOnlyCollection<WorkPause> pauses, DateTime now)
    {
        if (endedAt.HasValue) return (StCompleted, false, frozenUsed ?? Elapsed(startedAt, endedAt, pauses));
        if (!startedAt.HasValue) return (StNotStarted, false, null);
        var paused = pauses.Any(p => p.EndAt is null && WorkPauseClassifier.IsSimplePause(p) && p.StartAt >= startedAt.Value);
        return (paused ? StPaused : StInProgress, paused, Elapsed(startedAt, now, pauses));
    }

    /// <summary>TAT used = elapsed minus simple pauses (the shared canonical calculation); null when not started.</summary>
    private static int? Elapsed(DateTime? start, DateTime? end, IReadOnlyCollection<WorkPause> pauses) =>
        start.HasValue && end.HasValue && end.Value >= start.Value ? EaTaskService.CalculateActiveTatMinutes(start.Value, end.Value, pauses) : null;

    /// <summary>EaTask-based state (Meeting, Follow-up, Travel) using the shared TAT calculation.</summary>
    private static (string Status, bool Paused, int? Used) TaskState(EaTask? task, IReadOnlyCollection<WorkPause> pauses, DateTime now, DateTime? fallbackCompletedAt)
    {
        if (task is null) return (fallbackCompletedAt.HasValue ? StCompleted : StNotStarted, false, null);
        var paused = task.ExecutionStatus == EaTaskExecutionStatus.InProgress && pauses.Any(p => p.EndAt is null && WorkPauseClassifier.IsSimplePause(p));
        var status = task.ExecutionStatus switch
        {
            EaTaskExecutionStatus.Completed => StCompleted,
            EaTaskExecutionStatus.Cancelled => StCancelled,
            EaTaskExecutionStatus.InProgress => paused ? StPaused : StInProgress,
            _ => StNotStarted
        };
        int? used = status switch
        {
            StCancelled or StNotStarted => null,
            StCompleted => task.TatUsedMinutes ?? Elapsed(task.StartedAt, task.CompletedAt, pauses),
            _ => Elapsed(task.StartedAt, now, pauses),
        };
        return (status, paused, used);
    }

    /// <summary>TAT-only judgement: allotted vs used. Due dates are informational only.</summary>
    private static string Classify(WorkItem i, DateTime today)
    {
        if (i.Status == StCancelled) return PerfCancelled;
        if (i.Status == StNotStarted) return PerfPending;
        if (!i.AllottedTatMinutes.HasValue || !i.TatUsedMinutes.HasValue) return PerfNoTat;
        var over = i.TatUsedMinutes.Value > i.AllottedTatMinutes.Value;
        return i.Status == StCompleted ? (over ? PerfDelayed : PerfOnTime) : (over ? PerfOverdue : PerfInProgress);
    }

    // =================================================================================== KPI math

    private static EmKpiCellDto BuildCell(IEnumerable<WorkItem> source)
    {
        var items = source.ToList();
        var tat = BuildTat(items);
        var cell = new EmKpiCellDto
        {
            Planned = items.Count,
            Completed = items.Count(i => i.Status == StCompleted),
            OnTime = items.Count(i => i.Performance == PerfOnTime),
            Delayed = items.Count(i => i.Performance == PerfDelayed),
            InProgress = items.Count(i => i.Performance == PerfInProgress),
            Overdue = items.Count(i => i.Performance == PerfOverdue),
            Pending = items.Count(i => i.Performance == PerfPending),
            NoTat = items.Count(i => i.Performance == PerfNoTat),
            AllottedMinutes = tat.AllottedMinutes, UsedMinutes = tat.UsedMinutes, DifferenceMinutes = tat.DifferenceMinutes,
        };
        cell.NotCompleted = cell.Planned - cell.Completed;
        cell.NotCompletedPct = Pct(cell.NotCompleted, cell.Planned);
        cell.DelayedPct = Pct(cell.Delayed, cell.Completed);
        return cell;
    }

    private static List<EmBreakdownDto> Breakdown(IEnumerable<WorkItem> items, Func<WorkItem, string> key) =>
        items.GroupBy(key).Select(g => new EmBreakdownDto { Key = g.Key, Kpi = BuildCell(g) })
            .OrderByDescending(b => b.Kpi.Planned).ThenBy(b => b.Key, StringComparer.OrdinalIgnoreCase).ToList();

    private static List<EmCountDto> Counts<T>(IEnumerable<T> items, Func<T, string> key) =>
        items.GroupBy(key).Select(g => new EmCountDto { Key = g.Key, Count = g.Count() })
            .OrderByDescending(c => c.Count).ThenBy(c => c.Key, StringComparer.OrdinalIgnoreCase).ToList();

    private static EmTrendPointDto Point(IEnumerable<WorkItem> source)
    {
        var items = source.ToList();
        return new EmTrendPointDto
        {
            Planned = items.Count, Completed = items.Count(i => i.Status == StCompleted),
            OnTime = items.Count(i => i.Performance == PerfOnTime), Delayed = items.Count(i => i.Performance == PerfDelayed),
            Overdue = items.Count(i => i.Performance == PerfOverdue),
        };
    }

    private static EmTaskStatusDto BuildTaskStatus(IReadOnlyCollection<WorkItem> items)
    {
        var nonRework = items.Where(i => i.TaskType != TypeRework).ToList();
        return new EmTaskStatusDto
        {
            Total = items.Count,
            Rework = items.Count(i => i.TaskType == TypeRework),
            OnTime = nonRework.Count(i => i.Performance == PerfOnTime),
            Delayed = nonRework.Count(i => i.Performance == PerfDelayed),
            InProgress = nonRework.Count(i => i.Performance == PerfInProgress),
            Overdue = nonRework.Count(i => i.Performance == PerfOverdue),
            Pending = nonRework.Count(i => i.Performance == PerfPending),
            NoTat = nonRework.Count(i => i.Performance == PerfNoTat),
        };
    }

    /// <summary>TAT totals over the started/completed work that has an allotted TAT.</summary>
    private static EmTatDto BuildTat(IReadOnlyCollection<WorkItem> items)
    {
        var measured = items.Where(i => i.AllottedTatMinutes.HasValue && i.TatUsedMinutes.HasValue).ToList();
        var allotted = measured.Sum(i => i.AllottedTatMinutes!.Value);
        var used = measured.Sum(i => i.TatUsedMinutes!.Value);
        return new EmTatDto { MeasuredItems = measured.Count, AllottedMinutes = allotted, UsedMinutes = used, DifferenceMinutes = allotted - used, IsOver = used > allotted };
    }

    /// <summary>Fixed rules on the KPIs — no AI.</summary>
    private static EmFocusAreasDto BuildFocusAreas(EmKpiResponseDto r)
    {
        var s = r.Matrix.Summary;
        var focus = new EmFocusAreasDto();
        if (s.Planned == 0) return focus;
        var completionPct = 100m - s.NotCompletedPct;
        if (completionPct >= 90) focus.Strengths.Add("High completion rate");
        // TAT-based strengths only count when most completed work was actually judged on TAT.
        var judged = s.OnTime + s.Delayed;
        var mostlyJudged = judged > 0 && judged * 2 >= s.Completed;
        if (mostlyJudged && s.Delayed * 100m / judged <= 20) focus.Strengths.Add("Mostly completed on time");
        if (r.Matrix.Rework.Planned == 0 && s.Completed > 0) focus.Strengths.Add("No rework this week");
        if (mostlyJudged && r.Tat.MeasuredItems > 0 && !r.Tat.IsOver) focus.Strengths.Add("Within the allotted TAT");
        if (s.Delayed > 0) focus.Improve.Add($"Reduce delays ({s.Delayed} delayed)");
        if (s.Overdue > 0) focus.Improve.Add($"Finish work already over TAT ({s.Overdue} overdue)");
        if (s.Pending > 0) focus.Improve.Add($"Start pending work ({s.Pending} not started)");
        if (s.NoTat > 0) focus.Improve.Add($"Configure TAT for {s.NoTat} task(s) with no TAT");
        if (r.Matrix.Rework.Planned > 0) focus.Improve.Add($"Reduce rework ({r.Matrix.Rework.Planned} rework tasks)");
        if (r.Tat.IsOver) focus.Improve.Add("TAT used is over the allotted limit");
        if (r.CarryForwardOverdue > 0) focus.Improve.Add($"Close {r.CarryForwardOverdue} overdue task(s) from earlier weeks");
        return focus;
    }

    // =================================================================================== weeks

    /// <summary>ISO week of an India date. Sunday belongs to no week (weeks run Monday–Saturday).</summary>
    private static (int? Year, int? Week) WeekOf(DateTime indiaDate) =>
        indiaDate.DayOfWeek == DayOfWeek.Sunday ? (null, null) : (ISOWeek.GetYear(indiaDate), ISOWeek.GetWeekOfYear(indiaDate));

    private static (DateTime Start, DateTime End) WeekRange(int year, int week)
    {
        var monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
        return (monday, monday.AddDays(5));
    }

    private static (int Year, int Week) Shift(int year, int week, int weeks)
    {
        var monday = ISOWeek.ToDateTime(year, week, DayOfWeek.Monday).AddDays(7 * weeks);
        return (ISOWeek.GetYear(monday), ISOWeek.GetWeekOfYear(monday));
    }

    private static (int Year, int Week) ResolveWeek(int? year, int? week)
    {
        if (year is null && week is null)
        {
            var today = IndiaBusinessCalendar.ToIndiaDate(Clock.UtcNowTz);
            return (ISOWeek.GetYear(today), ISOWeek.GetWeekOfYear(today));
        }
        if (year is null || week is null) throw new BadRequestException("Year and Week must be sent together.");
        if (year is < 2000 or > 2100) throw new BadRequestException("Year must be between 2000 and 2100.");
        if (week < 1 || week > ISOWeek.GetWeeksInYear(year.Value))
            throw new BadRequestException($"Week must be between 1 and {ISOWeek.GetWeeksInYear(year.Value)} for {year}.");
        return (year.Value, week.Value);
    }

    // =================================================================================== helpers

    private static IEnumerable<WorkItem> Newest(IEnumerable<WorkItem> items) =>
        items.OrderByDescending(i => i.PlannedDate ?? DateTime.MinValue).ThenBy(i => i.ItemKey, StringComparer.Ordinal);

    private static T Fill<T>(T dto, WorkItem i) where T : EmWorkItemDto
    {
        dto.ItemKey = i.ItemKey; dto.Relation = i.Relation; dto.Category = i.Category; dto.Counterparty = i.Counterparty;
        dto.Module = i.Module; dto.TaskType = i.TaskType; dto.ReviewCycleNumber = i.ReviewCycleNumber;
        dto.RecordId = i.RecordId; dto.EaTaskId = i.EaTaskId; dto.ReferenceNo = i.ReferenceNo; dto.Title = i.Title;
        dto.OwnerId = i.OwnerId; dto.OwnerName = i.OwnerName; dto.PlannedDate = i.PlannedDate; dto.Year = i.Year; dto.Week = i.Week;
        dto.DueDate = i.DueDate; dto.StartedAt = i.StartedAt; dto.CompletedAt = i.CompletedAt; dto.Status = i.Status; dto.IsPaused = i.IsPaused;
        dto.Performance = i.Performance; dto.AllottedTatMinutes = i.AllottedTatMinutes; dto.TatUsedMinutes = i.TatUsedMinutes;
        dto.TatDifferenceMinutes = i.AllottedTatMinutes.HasValue && i.TatUsedMinutes.HasValue ? i.AllottedTatMinutes - i.TatUsedMinutes : null;
        dto.PauseCount = i.PauseCount; dto.PausedMinutes = i.PausedMinutes;
        dto.Type = i.Type ?? i.Category; dto.Subtype = i.Subtype; dto.AssignedByName = i.AssignedByName;
        dto.AssigneeName = i.AssigneeName; dto.DoerName = i.DoerName ?? i.OwnerName; dto.Priority = i.Priority; dto.Description = i.Description;
        var now = Clock.UtcNowTz;
        dto.Pauses = i.PauseList.OrderBy(p => p.StartAt).Select(p => new EmItemPauseDto
        {
            StartAt = p.StartAt, EndAt = p.EndAt, Minutes = (int)((p.EndAt ?? now) - p.StartAt).TotalMinutes, Reason = p.Reason
        }).ToList();
        return dto;
    }

    /// <summary>
    /// Record-level attachments (module, entity) → the Documents of the one phase item each file belongs to.
    /// Delegation / approval files are stored per record, but each is uploaded at the moment a phase ends
    /// (completion PDF ends Actual/Rework, review/rework decision file ends a Review cycle), so a file is
    /// matched against ALL of the record's phases (<paramref name="allItems"/>, not just this page) and
    /// only shown on its own row. One query for the whole list.
    /// </summary>
    private async Task AttachDocumentsAsync(IReadOnlyCollection<EmWorkItemDto> dtos, IReadOnlyCollection<WorkItem> allItems, CancellationToken ct)
    {
        static (string Module, string Entity)? Target(string module) => module switch
        {
            ModDelegation => ("Delegation", "Delegation"),
            ModApproval => ("Approval", "ApprovalRequest"),
            ModMeeting => ("Meeting", "Meeting"),
            ModTravel => ("Travel", "TravelRequest"),
            _ => null,
        };
        var keyed = dtos.Select(d => (Dto: d, Target: Target(d.Module), Id: d.RecordId.ToString(CultureInfo.InvariantCulture)))
            .Where(x => x.Target is not null).ToList();
        if (keyed.Count == 0) return;
        var modules = keyed.Select(x => x.Target!.Value.Module).Distinct().ToList();
        var ids = keyed.Select(x => x.Id).Distinct().ToList();
        var rows = await db.Attachments.AsNoTracking()
            .Where(a => a.IsActive && !a.IsDeleted && a.RelatedModule != null && modules.Contains(a.RelatedModule)
                && a.RelatedEntityId != null && ids.Contains(a.RelatedEntityId))
            .OrderBy(a => a.UploadedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);
        var byRecord = rows.GroupBy(a => (a.RelatedModule, a.RelatedEntity, a.RelatedEntityId)).ToDictionary(g => g.Key, g => g.ToList());
        var phasesByRecord = allItems.GroupBy(i => (i.Module, i.RecordId)).ToDictionary(g => g.Key, g => g.ToList());

        // itemKey → its files
        var owned = new Dictionary<string, List<Attachment>>();
        foreach (var record in keyed.Select(x => (x.Dto.Module, x.Dto.RecordId, x.Target, x.Id)).Distinct())
        {
            if (!byRecord.TryGetValue((record.Target!.Value.Module, record.Target.Value.Entity, record.Id), out var docs)) continue;
            var phases = phasesByRecord.GetValueOrDefault((record.Module, record.RecordId)) ?? [];
            foreach (var doc in docs)
            {
                // No phase window fits (e.g. a supporting file added before work started) → the record's first (Actual) row.
                var owner = (phases.Count <= 1 ? null : OwningPhase(doc, phases))
                    ?? phases.OrderBy(p => p.ReviewCycleNumber).ThenBy(p => p.TaskType == TypeActual ? 0 : 1).FirstOrDefault();
                if (owner is null) continue;
                if (!owned.TryGetValue(owner.ItemKey, out var list)) owned[owner.ItemKey] = list = [];
                list.Add(doc);
            }
        }
        foreach (var (dto, _, _) in keyed)
        {
            if (!owned.TryGetValue(dto.ItemKey, out var docs)) continue;
            dto.Documents = docs.Select(a => new EmItemDocumentDto
            {
                AttachmentId = a.Id, FileName = a.OriginalFileName, ContentType = a.ContentType, Size = a.Size,
                UploadedBy = a.UploadedBy, UploadedAt = a.UploadedAt
            }).ToList();
        }
    }

    /// <summary>
    /// The phase a file was uploaded to close: a started phase whose end is at/after the upload (or still open),
    /// nearest end first. The file's metadata narrows the candidates — a review/rework decision file belongs to a
    /// Review phase (of its cycle when recorded), a completion file to an Actual/Rework phase. Null when nothing fits.
    /// </summary>
    private static WorkItem? OwningPhase(Attachment doc, List<WorkItem> phases)
    {
        var (purpose, cycle) = ReadAttachmentMeta(doc.Metadata);
        var isDecision = purpose is not null && (purpose.Contains("Review", StringComparison.OrdinalIgnoreCase)
            || purpose.Contains("RejectAttachment", StringComparison.OrdinalIgnoreCase)
            || (purpose.Contains("Rework", StringComparison.OrdinalIgnoreCase) && purpose.Contains("Attachment", StringComparison.OrdinalIgnoreCase)));
        var isCompletion = purpose is not null && !isDecision && purpose.Contains("Completion", StringComparison.OrdinalIgnoreCase);
        IEnumerable<WorkItem> candidates = phases;
        if (isDecision) candidates = candidates.Where(p => p.TaskType == TypeReview && (cycle is null || p.ReviewCycleNumber == cycle));
        else if (isCompletion) candidates = candidates.Where(p => p.TaskType != TypeReview);

        var tolerance = TimeSpan.FromMinutes(1);
        var up = doc.UploadedAt;
        return candidates
            .Where(p => p.StartedAt is null || p.StartedAt <= up + tolerance)
            .Where(p => p.CompletedAt is null || p.CompletedAt >= up - tolerance)
            .OrderBy(p => p.CompletedAt is null ? 1 : 0)
            .ThenBy(p => p.CompletedAt is null ? TimeSpan.MaxValue : (p.CompletedAt.Value - up).Duration())
            .FirstOrDefault();
    }

    private static (string? Purpose, int? Cycle) ReadAttachmentMeta(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata)) return (null, null);
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(metadata);
            var root = json.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return (null, null);
            var purpose = root.TryGetProperty("purpose", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String ? p.GetString() : null;
            int? cycle = root.TryGetProperty("reviewCycleNumber", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Number && c.TryGetInt32(out var n) ? n : null;
            return (purpose, cycle);
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, null);
        }
    }

    private static string? Allowed(string? value, string[] allowed, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return allowed.FirstOrDefault(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BadRequestException($"{field} must be one of: {string.Join(", ", allowed)}.");
    }

    private static decimal Pct(int part, int whole) => whole == 0 ? 0 : decimal.Round(part * 100m / whole, 2);
    private static bool Contains(string? text, string term) => text?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
