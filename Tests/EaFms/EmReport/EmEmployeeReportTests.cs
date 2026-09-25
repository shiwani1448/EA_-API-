using System;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.EmReport;

/// <summary>
/// EM Employee Report: one employee's ISO week (Mon–Sat, India time) across every module, judged on
/// TAT only — allotted vs used (elapsed minus pauses). Dates are fixed in Week 37/38 of 2026.
/// </summary>
public class EmEmployeeReportTests
{
    private const string Emp = "S5I-1";
    private const string EmpName = "Jay Pujari";
    private const int Huge = 10_000_000; // a TAT large enough that open work stays within it
    // India time on each day, expressed in UTC.
    private static DateTime Ist(int month, int day, int hour = 12) => new DateTime(2026, month, day, hour, 0, 0, DateTimeKind.Utc).AddHours(-5.5);

    /// <summary>No login: the query decides who the report is for.</summary>
    private static EmEmployeeReportService Svc(EaFmsDbContext db, string? loginId = null, string? loginName = null) =>
        new(db, Jarvis5.Tests.EaFms.Followups.FollowupTestSupport.User(loginId, loginName));

    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DelegationEntity Delegation(long id, string doerId, string doerName, DateTime? due, string status) => new()
    {
        Id = id, ReferenceNo = $"DLG-{id}", EaTaskId = 1000 + id, Title = $"Delegation {id}", DoerId = doerId, DoerNameSnapshot = doerName,
        AssignedById = "boss", Status = status, DueDate = due, CreatedBy = "seed", CreatedDate = Ist(9, 1),
    };

    private static DelegationPhaseTat Phase(long delegationId, string type, int cycle, DateTime? started, DateTime? ended, int? allotted, int? used) => new()
    {
        DelegationId = delegationId, TaskType = type, ReviewCycleNumber = cycle, StartedAt = started, EndedAt = ended,
        AllottedTatMinutes = allotted, TatUsedMinutes = used, CreatedBy = "seed", CreatedDate = started ?? Ist(9, 14),
    };

    private static EaTask Task(long id, string module, string record, string status, int? allotted, DateTime? started = null, DateTime? completed = null, int? used = null) => new()
    {
        Id = id, BusinessModuleId = 1, ModuleName = module, BusinessRecordId = record, Task = record, ExecutionStatus = status,
        AllottedTatMinutes = allotted, TatUsedMinutes = used, StartedAt = started, CompletedAt = completed, CreatedBy = "seed", CreatedDate = Ist(9, 1),
    };

    /// <summary>
    /// Week 38 (Mon 14 – Sat 19 Sep 2026) for S5I-1 — everything judged on TAT:
    ///   D1 Actual   completed, used 30 / 60             → OnTime     (Actual)
    ///   D1 Review   completed, used 45 / 30             → Delayed    (Review)
    ///   D1 Rework   open since 15 Sep, TAT 10            → Overdue    (Rework)
    ///   D3 Actual   not started (TAT 60 predefined)      → Pending    (Actual)
    ///   Approval    raised by "jay  PUJARI", used 100/120 → OnTime   (Actual, matched by name)
    ///   Meeting     doer S5I-1, used 90 / 60             → Delayed    (Meeting)
    ///   Follow-up   started 16 Sep, huge TAT             → InProgress (Actual)
    ///   Travel      completed in 1 day, no TAT allotted  → NoTat      (Actual; used still shown: 1440)
    /// Excluded: D2 (other doer); a Follow-up planned on Sunday 20 Sep (weeks are Mon–Sat).
    /// Week 37: D4 completed on time (baseline for deltas).
    /// </summary>
    private static async Task<EaFmsDbContext> SeedAsync()
    {
        var db = NewDb();
        db.Delegations.AddRange(
            Delegation(1, Emp, EmpName, Ist(9, 18), DelegationStatus.InProgress),
            Delegation(2, "OTHER", "Someone Else", Ist(9, 18), DelegationStatus.Pending),
            Delegation(3, Emp, EmpName, Ist(9, 16), DelegationStatus.Pending),
            Delegation(4, Emp, EmpName, Ist(9, 10), DelegationStatus.Completed));
        db.Tasks.AddRange(
            Task(1003, "Delegation", "3", EaTaskExecutionStatus.NotStarted, 60),
            Task(1004, "Delegation", "4", EaTaskExecutionStatus.Completed, 60));
        db.DelegationPhaseTats.AddRange(
            Phase(1, "Actual", 0, Ist(9, 14, 10), Ist(9, 14, 11), 60, 30),
            Phase(1, "Review", 1, Ist(9, 15, 9), Ist(9, 15, 10), 30, 45),
            Phase(1, "Rework", 1, Ist(9, 15, 11), null, 10, null),
            Phase(4, "Actual", 0, Ist(9, 8, 10), Ist(9, 8, 11), 60, 20));

        db.ApprovalRequests.Add(new ApprovalRequest { Id = 10, ReferenceNo = "APR-10", EaTaskId = 2010, RequestTitle = "Laptop", RequestedBy = "jay  PUJARI",
            WorkflowStatus = "Approved", RequiredApprovalDate = Ist(9, 18), CreatedBy = "ea", CreatedAt = Ist(9, 10) });
        db.ApprovalPhaseTats.Add(new ApprovalPhaseTat { ApprovalRequestId = 10, TaskType = "Actual", ReviewCycleNumber = 0,
            StartedAt = Ist(9, 15, 9), EndedAt = Ist(9, 16, 9), AllottedTatMinutes = 120, TatUsedMinutes = 100, CreatedBy = "seed", CreatedDate = Ist(9, 15) });

        db.Meetings.Add(new Meeting { Id = 20, MeetingNumber = "MTG-20", Title = "Board", StartDateTime = Ist(9, 17),
            DoerIds = [Emp, "S5I-2"], DoerNames = [EmpName, "Other Doer"], CreatedBy = "seed", CreatedDate = Ist(9, 1) });
        db.Tasks.Add(Task(3020, "Meeting", "20", EaTaskExecutionStatus.Completed, 60, Ist(9, 17, 10), Ist(9, 17, 12), used: 90));

        db.Tasks.AddRange(
            Task(4030, "Follow-up", "30", EaTaskExecutionStatus.InProgress, Huge, started: Ist(9, 16)),
            Task(4031, "Follow-up", "31", EaTaskExecutionStatus.NotStarted, 60),
            Task(5040, "Travel & Hospitality", "40", EaTaskExecutionStatus.Completed, null, Ist(9, 14), Ist(9, 15)));
        db.Followups.AddRange(
            new Followup { Id = 30, EaTaskId = 4030, Subject = "Chase vendor", DoerId = Emp, DoerName = EmpName, DueAt = Ist(9, 17), CreatedBy = "seed", CreatedDate = Ist(9, 1) },
            new Followup { Id = 31, EaTaskId = 4031, Subject = "Sunday item", DoerId = Emp, DoerName = EmpName, DueAt = Ist(9, 20), CreatedBy = "seed", CreatedDate = Ist(9, 1) });
        db.TravelRequests.Add(new TravelRequest { Id = 40, ReferenceNo = "TRV-40", EaTaskId = 5040, Purpose = "Client visit", RequiredDate = Ist(9, 18),
            CreatedBy = "Jay Pujari", CreatedDate = Ist(9, 1) });
        await db.SaveChangesAsync();
        return db;
    }

    private static EmEmployeeReportQueryDto Week38 => new() { EmployeeId = Emp, EmployeeName = EmpName, Year = 2026, Week = 38 };

    [Fact]
    public async Task Kpis_Matrix_IsJudgedOnTatOnly_AcrossEveryModule()
    {
        await using var db = await SeedAsync();

        var r = await Svc(db).GetKpisAsync(Week38, default);

        Assert.Equal((new DateTime(2026, 9, 14), new DateTime(2026, 9, 19)), (r.WeekStart, r.WeekEnd)); // Mon–Sat
        var s = r.Matrix.Summary;
        Assert.Equal((8, 5, 3), (s.Planned, s.Completed, s.NotCompleted));
        Assert.Equal((2, 2, 1, 1, 1, 1), (s.OnTime, s.Delayed, s.InProgress, s.Overdue, s.Pending, s.NoTat));
        Assert.Equal((37.5m, 40m), (s.NotCompletedPct, s.DelayedPct));

        var a = r.Matrix.Actual;   // D1 Actual, D3, Approval, Follow-up, Travel
        Assert.Equal((5, 3, 2, 0, 1, 1, 1), (a.Planned, a.Completed, a.OnTime, a.Delayed, a.InProgress, a.Pending, a.NoTat));
        Assert.Equal((1, 1, 1), (r.Matrix.Review.Planned, r.Matrix.Review.Completed, r.Matrix.Review.Delayed));
        Assert.Equal((1, 0, 1), (r.Matrix.Rework.Planned, r.Matrix.Rework.Completed, r.Matrix.Rework.Overdue));
        Assert.Equal((1, 1, 1), (r.Matrix.Meeting.Planned, r.Matrix.Meeting.Completed, r.Matrix.Meeting.Delayed));

        // Per-column TAT: Review 30 allotted / 45 used; Meeting 60 / 90.
        Assert.Equal((30, 45, -15), (r.Matrix.Review.AllottedMinutes, r.Matrix.Review.UsedMinutes, r.Matrix.Review.DifferenceMinutes));
        Assert.Equal((60, 90, -30), (r.Matrix.Meeting.AllottedMinutes, r.Matrix.Meeting.UsedMinutes, r.Matrix.Meeting.DifferenceMinutes));
    }

    [Fact]
    public async Task Kpis_Modules_TaskStatus_Tat_Deltas_AndFocusAreas()
    {
        await using var db = await SeedAsync();

        var r = await Svc(db).GetKpisAsync(Week38, default);

        var byModule = r.Modules.ToDictionary(m => m.Module, m => m.Kpi.Planned);
        Assert.Equal((4, 1, 1, 1, 1), (byModule["Delegation"], byModule["Approval"], byModule["Meeting"], byModule["Follow-up"], byModule["Travel"]));

        var t = r.TaskStatus;
        Assert.Equal((8, 2, 2, 1, 0, 1, 1, 1), (t.Total, t.OnTime, t.Delayed, t.InProgress, t.Overdue, t.Pending, t.Rework, t.NoTat));

        // TAT totals: every started/completed item that has an allotted TAT (D1 ×3, Approval, Meeting, Follow-up).
        Assert.Equal(6, r.Tat.MeasuredItems);
        Assert.Equal(r.Tat.AllottedMinutes - r.Tat.UsedMinutes, r.Tat.DifferenceMinutes);
        Assert.Equal(r.Tat.UsedMinutes > r.Tat.AllottedMinutes, r.Tat.IsOver);   // the huge Follow-up TAT keeps the week within TAT

        // Week 37 had one on-time completion (D4).
        Assert.Equal((7, 4, 1, 2), (r.Matrix.Summary.Delta.Planned, r.Matrix.Summary.Delta.Completed, r.Matrix.Summary.Delta.OnTime, r.Matrix.Summary.Delta.Delayed));

        Assert.Contains("Reduce delays (2 delayed)", r.FocusAreas.Improve);
        Assert.Contains("Finish work already over TAT (1 overdue)", r.FocusAreas.Improve);
        Assert.Contains("Start pending work (1 not started)", r.FocusAreas.Improve);
        Assert.Contains("Configure TAT for 1 task(s) with no TAT", r.FocusAreas.Improve);
    }

    [Fact]
    public async Task WorkItems_ShowAllottedUsedAndDifference_ForEveryModule()
    {
        await using var db = await SeedAsync();
        var svc = Svc(db);

        var all = await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, EmployeeName = EmpName, Year = 2026, Week = 38 }, default);
        Assert.Equal(8, all.TotalCount);
        Assert.Equal(new[] { "Approval", "Delegation", "Follow-up", "Meeting", "Travel" }, all.Items.Select(i => i.Module).Distinct().OrderBy(x => x));
        var byKey = all.Items.ToDictionary(i => i.ItemKey);

        Assert.Equal(("OnTime", 60, 30, 30), (byKey["Delegation:1:Actual:0"].Performance, byKey["Delegation:1:Actual:0"].AllottedTatMinutes!.Value, byKey["Delegation:1:Actual:0"].TatUsedMinutes!.Value, byKey["Delegation:1:Actual:0"].TatDifferenceMinutes!.Value));
        Assert.Equal(("Delayed", -15), (byKey["Delegation:1:Review:1"].Performance, byKey["Delegation:1:Review:1"].TatDifferenceMinutes!.Value));
        Assert.Equal("Overdue", byKey["Delegation:1:Rework:1"].Performance);
        Assert.Equal(("Pending", 60, (int?)null), (byKey["Delegation:3:Actual:0"].Performance, byKey["Delegation:3:Actual:0"].AllottedTatMinutes!.Value, byKey["Delegation:3:Actual:0"].TatUsedMinutes));
        Assert.Equal("OnTime", byKey["Approval:10:Actual:0"].Performance);
        var meeting = byKey["Meeting:20:Meeting:0"];
        Assert.Equal(("Delayed", -30, "Doer", "Jay Pujari, Other Doer"), (meeting.Performance, meeting.TatDifferenceMinutes!.Value, meeting.Relation, meeting.Counterparty));
        Assert.Equal("InProgress", byKey["Follow-up:30:Actual:0"].Performance);
        // No TAT allotted: time used is still shown, but it is not judged.
        var travel = byKey["Travel:40:Actual:0"];
        Assert.Equal(("NoTat", (int?)null, 1440, (int?)null), (travel.Performance, travel.AllottedTatMinutes, travel.TatUsedMinutes!.Value, travel.TatDifferenceMinutes));

        var overdue = await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, EmployeeName = EmpName, AllWeeks = true, Performance = "overdue" }, default);
        Assert.Equal(new[] { "Delegation:1:Rework:1" }, overdue.Items.Select(i => i.ItemKey));

        var everything = await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, EmployeeName = EmpName, AllWeeks = true, PageSize = 200 }, default);
        Assert.Contains(everything.Items, i => i.ItemKey == "Delegation:4:Actual:0" && i.Week == 37);
        Assert.Contains(everything.Items, i => i.ItemKey == "Follow-up:31:Actual:0" && i.Week == null);   // Sunday: no week
    }

    [Fact]
    public async Task PausedTime_IsExcludedFromTatUsed()
    {
        await using var db = await SeedAsync();
        // A 30-minute pause during the Follow-up: used drops by exactly 30 minutes.
        var before = (await Svc(db).GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, Year = 2026, Week = 38, Module = "Follow-up" }, default)).Items.Single().TatUsedMinutes!.Value;
        var workflow = new WorkflowInstance { BusinessModuleId = 1, BusinessRecordId = "30", CreatedBy = "seed", CreatedDate = Ist(9, 1) };
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();
        var task = await db.Tasks.SingleAsync(t => t.Id == 4030);
        task.WorkflowInstanceId = workflow.Id;
        db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = workflow.Id, StartAt = Ist(9, 16, 13), EndAt = Ist(9, 16, 13).AddMinutes(30), CreatedBy = "seed", CreatedDate = Ist(9, 16) });
        await db.SaveChangesAsync();

        var after = (await Svc(db).GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, Year = 2026, Week = 38, Module = "Follow-up" }, default)).Items.Single().TatUsedMinutes!.Value;

        Assert.InRange(before - after, 29, 31);
    }

    [Fact]
    public async Task ApprovalAndTravel_AreMatchedByName_OtherDoersAreExcluded_AndTeamViewIncludesEveryone()
    {
        await using var db = await SeedAsync();
        var svc = Svc(db);

        var byName = await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeName = "  JAY   pujari ", Year = 2026, Week = 38 }, default);
        Assert.Contains(byName.Items, i => i.Module == "Approval");
        Assert.Contains(byName.Items, i => i.Module == "Travel");
        Assert.DoesNotContain(byName.Items, i => i.RecordId == 2 && i.Module == "Delegation");

        var team = await svc.GetKpisAsync(new EmEmployeeReportQueryDto { Year = 2026, Week = 38 }, default);
        Assert.Equal(9, team.Matrix.Summary.Planned);      // + D2; the meeting counts once in the team view
    }

    [Fact]
    public async Task Trends_ReturnsTheRequestedWeeks_OldestFirst()
    {
        await using var db = await SeedAsync();

        var r = await Svc(db).GetTrendsAsync(new EmEmployeeTrendQueryDto { EmployeeId = Emp, EmployeeName = EmpName, Year = 2026, Week = 38, Weeks = 3 }, default);

        Assert.Equal(new[] { "W36", "W37", "W38" }, r.Weeks.Select(w => w.Label));
        Assert.Equal((1, 1), (r.Weeks[1].Summary.Completed, r.Weeks[1].Actual.OnTime));
        Assert.Equal((8, 5, 2, 2, 1), (r.Weeks[2].Summary.Planned, r.Weeks[2].Summary.Completed, r.Weeks[2].Summary.OnTime, r.Weeks[2].Summary.Delayed, r.Weeks[2].Summary.Overdue));
    }

    [Theory]
    [InlineData(2026, 54)]
    [InlineData(1999, 10)]
    public async Task InvalidWeek_Is400(int year, int week)
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<BadRequestException>(() => Svc(db).GetKpisAsync(new EmEmployeeReportQueryDto { Year = year, Week = week }, default));
    }

    [Fact]
    public async Task InvalidFilters_Are400()
    {
        await using var db = NewDb();
        var svc = Svc(db);
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetWorkItemsAsync(new EmWorkItemQueryDto { Performance = "NotMeasured" }, default));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetWorkItemsAsync(new EmWorkItemQueryDto { PageSize = 500 }, default));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetKpisAsync(new EmEmployeeReportQueryDto { Year = 2026 }, default));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetTrendsAsync(new EmEmployeeTrendQueryDto { Weeks = 13 }, default));
    }

    // ------------------------------------------------------------------ login identity & sections

    [Fact]
    public async Task NoEmployeeInTheQuery_UsesTheLoggedInEa_AndTeamTrueShowsEveryone()
    {
        await using var db = await SeedAsync();

        var mine = await Svc(db, loginId: Emp, loginName: EmpName).GetKpisAsync(new EmEmployeeReportQueryDto { Year = 2026, Week = 38 }, default);
        Assert.Equal((Emp, EmpName, 8), (mine.EmployeeId, mine.EmployeeName, mine.Matrix.Summary.Planned));

        var team = await Svc(db, loginId: Emp, loginName: EmpName).GetKpisAsync(new EmEmployeeReportQueryDto { Year = 2026, Week = 38, Team = true }, default);
        Assert.Equal(9, team.Matrix.Summary.Planned);

        // Another EA logged in sees only her own work (none here).
        var other = await Svc(db, loginId: "S5I-9", loginName: "Shivani").GetKpisAsync(new EmEmployeeReportQueryDto { Year = 2026, Week = 38 }, default);
        Assert.Equal(0, other.Matrix.Summary.Planned);
    }

    /// <summary>Extra rows for the EA's whole tracking (Week 38).</summary>
    private static async Task AddTrackingAsync(EaFmsDbContext db)
    {
        // A delegation SHE assigned (to Riya), type "Report", not started.
        db.Delegations.Add(new DelegationEntity { Id = 5, ReferenceNo = "DLG-5", EaTaskId = 1005, Title = "Prepare report", DoerId = "E-9", DoerNameSnapshot = "Riya",
            AssignedById = EmpName, AssignedByNameSnapshot = EmpName, DelegationType = "Report", Status = DelegationStatus.Pending, DueDate = Ist(9, 18),
            CreatedBy = EmpName, CreatedDate = Ist(9, 14) });
        db.Tasks.Add(Task(1005, "Delegation", "5", EaTaskExecutionStatus.NotStarted, 120));
        // A meeting she only attended.
        db.Meetings.Add(new Meeting { Id = 21, MeetingNumber = "MTG-21", Title = "Vendor sync", StartDateTime = Ist(9, 18), MeetingType = "External",
            DoerIds = ["S5I-2"], DoerNames = ["Other Doer"], OrganizerName = "Boss", CreatedBy = "Boss", CreatedDate = Ist(9, 1) });
        db.MeetingAttendees.Add(new MeetingAttendee { MeetingId = 21, ParticipantId = Emp, ParticipantName = EmpName, AttendanceStatus = "Attended",
            AttendedAt = Ist(9, 18), CreatedBy = "seed", CreatedDate = Ist(9, 1) });
        // Follow-up 30: two attempts (with times), one email reminder, one open escalation, waiting on Acme.
        var followup = await db.Followups.SingleAsync(f => f.Id == 30);
        followup.WaitingOnName = "Acme";
        followup.Type = "Vendor";
        db.FollowupCycles.AddRange(
            new FollowupCycle { FollowupId = 30, SequenceNumber = 1, FollowedUpAt = Ist(9, 16, 10), Note = "Called", FollowedUpByEmployeeId = Emp, FollowedUpByEmployeeName = EmpName, CreatedBy = EmpName, CreatedDate = Ist(9, 16) },
            new FollowupCycle { FollowupId = 30, SequenceNumber = 2, FollowedUpAt = Ist(9, 17, 15), Note = "Emailed again", FollowedUpByEmployeeId = Emp, FollowedUpByEmployeeName = EmpName, CreatedBy = EmpName, CreatedDate = Ist(9, 17) });
        db.FollowupReminderLogs.Add(new FollowupReminderLog { FollowupId = 30, Channel = "Email", Recipient = "vendor@acme.com", RecipientName = "Acme",
            Message = "Please reply", SentAt = Ist(9, 16, 11), SentById = Emp, SentByName = EmpName, CreatedDate = Ist(9, 16) });
        db.Escalations.Add(new Escalation { FollowupId = 30, EscalationLevelId = 1, InitiatedAt = Ist(9, 17), CreatedBy = "seed", CreatedDate = Ist(9, 17) });
        // A document she uploaded.
        db.Attachments.Add(new Attachment { RelatedModule = "Meeting", RelatedEntity = "Meeting", RelatedEntityId = "20", OriginalFileName = "mom.pdf",
            ObjectKey = "k", Size = 10, UploadedBy = EmpName, UploadedAt = Ist(9, 16), IsActive = true, CreatedBy = EmpName, CreatedDate = Ist(9, 16) });
        // A 30-minute pause on the follow-up.
        var workflow = new WorkflowInstance { BusinessModuleId = 1, BusinessRecordId = "30", CreatedBy = "seed", CreatedDate = Ist(9, 1) };
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();
        (await db.Tasks.SingleAsync(t => t.Id == 4030)).WorkflowInstanceId = workflow.Id;
        db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = workflow.Id, StartAt = Ist(9, 16, 13), EndAt = Ist(9, 16, 13).AddMinutes(30), Reason = "Waiting on vendor",
            CreatedBy = "seed", CreatedDate = Ist(9, 16) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Sections_ShowTheEasWholeTracking_ModuleByModule()
    {
        await using var db = await SeedAsync();
        await AddTrackingAsync(db);

        var r = await Svc(db, loginId: Emp, loginName: EmpName).GetSectionsAsync(new EmSectionsQueryDto { Year = 2026, Week = 38 }, default);

        Assert.False(r.IsTeamView);
        Assert.Equal((new DateTime(2026, 9, 14), new DateTime(2026, 9, 19)), (r.WeekStart!.Value, r.WeekEnd!.Value));

        // Delegations: D1 (3 phases) + D3 assigned to her, D5 assigned by her to Riya.
        var d = r.Delegations;
        Assert.Equal((5, 3, 1, 4, 1), (d.Kpi.Planned, d.DelegationCount, d.DelegatedByMe.Planned, d.DelegatedToMe.Planned, d.ReviewCycles));
        Assert.Equal(1, d.ByType.Single(b => b.Key == "Report").Kpi.Planned);
        Assert.Equal(("Riya", 1), (d.ByDoer.Single().Key, d.ByDoer.Single().Kpi.Planned));
        var assigned = d.Items.Single(i => i.RecordId == 5);
        Assert.Equal(("DelegatedByMe", "Riya", "Report", "Pending"), (assigned.Relation, assigned.Counterparty, assigned.Category, assigned.Performance));
        Assert.Equal(3, d.ByPhase.Count);   // Actual, Review, Rework

        // Meetings: one as doer, one attended.
        var m = r.Meetings;
        Assert.Equal((2, 1, 1), (m.Kpi.Planned, m.AsDoer, m.Attended));
        var attended = m.Items.Single(i => i.RecordId == 21);
        Assert.Equal(("Attendee", "Attended", "Boss"), (attended.Relation, attended.AttendanceStatus, attended.OrganizerName));

        // Follow-ups: when, what time, to whom.
        var f = r.Followups;
        Assert.Equal((1, 2, 1, 1, 1), (f.Kpi.Planned, f.Attempts, f.RemindersSent, f.Escalations, f.OpenEscalations));
        Assert.Equal(("Acme", 1), (f.ByRecipient.Single().Key, f.ByRecipient.Single().Count));
        var row = f.Items.Single();
        Assert.Equal(new[] { Ist(9, 16, 10), Ist(9, 17, 15) }, row.AttemptLog.Select(a => a.FollowedUpAt));
        Assert.Equal(("Email", "vendor@acme.com", Ist(9, 16, 11)), (row.ReminderLog.Single().Channel, row.ReminderLog.Single().Recipient, row.ReminderLog.Single().SentAt));
        Assert.Equal(("Acme", 1, 1, 30), (row.WaitingOn, row.OpenEscalations, row.PauseCount, row.PausedMinutes));

        // Approvals, travel, documents, pauses.
        Assert.Equal((1, 1), (r.Approvals.RequestCount, r.Approvals.ByStatus.Single(s => s.Key == "Approved").Count));
        Assert.Equal(1, r.Travel.Kpi.Planned);
        Assert.Equal((1, "mom.pdf"), (r.Documents.Total, r.Documents.Items.Single().FileName));
        Assert.Equal((1, 30, "Waiting on vendor"), (r.Pauses.Count, r.Pauses.TotalMinutes, r.Pauses.TopReasons.Single().Key));

        // Cumulative summary = every section added together.
        Assert.Equal(5 + 2 + 1 + 1 + 1, r.Summary.Kpi.Planned);
        Assert.Equal((1, 30, 1), (r.Summary.PauseCount, r.Summary.PausedMinutes, r.Summary.DocumentCount));
        Assert.Equal(r.Summary.Kpi.Planned, r.Summary.ByModule.Sum(b => b.Kpi.Planned));
    }

    [Fact]
    public async Task Sections_AllWeeks_IsCumulative_AndMaxRowsOnlyCapsTheLists()
    {
        await using var db = await SeedAsync();
        await AddTrackingAsync(db);
        var svc = Svc(db, loginId: Emp, loginName: EmpName);

        var all = await svc.GetSectionsAsync(new EmSectionsQueryDto { AllWeeks = true, MaxRows = 1 }, default);

        Assert.True(all.AllWeeks);
        Assert.Null(all.Week);
        Assert.Equal(6, all.Delegations.Kpi.Planned);      // + D4 from Week 37
        Assert.Equal(6, all.Delegations.TotalRows);
        Assert.Single(all.Delegations.Items);               // capped list, full counts
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetSectionsAsync(new EmSectionsQueryDto { MaxRows = 0 }, default));
    }
}
