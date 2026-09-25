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
/// EM Employee Report: one employee's ISO week (Mon–Sat, India time) across every module.
/// All dates are fixed in Week 37/38 of 2026, which is in the past, so open work with a due date is Overdue.
/// </summary>
public class EmEmployeeReportTests
{
    private const string Emp = "S5I-1";
    private const string EmpName = "Jay Pujari";
    // India noon on each day, expressed in UTC.
    private static DateTime Ist(int month, int day, int hour = 12) => new DateTime(2026, month, day, hour, 0, 0, DateTimeKind.Utc).AddHours(-5.5);

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

    /// <summary>
    /// Week 38 (Mon 14 – Sat 19 Sep 2026) for S5I-1:
    ///   D1 Actual   completed, 30/60 min            → OnTime   (Actual)
    ///   D1 Review   completed, 45/30 min            → Delayed  (Review)
    ///   D1 Rework   open since 15 Sep, 10 min TAT   → Overdue  (Rework)
    ///   D3 Actual   not started, due 16 Sep          → Overdue  (Actual)
    ///   Approval    raised by "jay  PUJARI", done before its due date → OnTime (Actual, matched by name)
    ///   Meeting     doer S5I-1, 90/60 min           → Delayed  (Meeting)
    ///   Follow-up   not started, due 17 Sep          → Overdue  (Actual)
    ///   Travel      by "Jay Pujari", done before required date → OnTime (Actual, matched by name)
    /// Excluded: D2 (other doer), a Follow-up due on Sunday 20 Sep (weeks are Mon–Sat).
    /// Week 37: D4 completed on time (the "previous week" baseline for deltas).
    /// </summary>
    private static async Task<EaFmsDbContext> SeedAsync()
    {
        var db = NewDb();
        db.Delegations.AddRange(
            Delegation(1, Emp, EmpName, Ist(9, 18), DelegationStatus.InProgress),
            Delegation(2, "OTHER", "Someone Else", Ist(9, 18), DelegationStatus.Pending),
            Delegation(3, Emp, EmpName, Ist(9, 16), DelegationStatus.Pending),
            Delegation(4, Emp, EmpName, Ist(9, 10), DelegationStatus.Completed));
        db.DelegationPhaseTats.AddRange(
            Phase(1, "Actual", 0, Ist(9, 14, 10), Ist(9, 14, 11), 60, 30),
            Phase(1, "Review", 1, Ist(9, 15, 9), Ist(9, 15, 10), 30, 45),
            Phase(1, "Rework", 1, Ist(9, 15, 11), null, 10, null),
            Phase(4, "Actual", 0, Ist(9, 8, 10), Ist(9, 8, 11), null, null));

        db.ApprovalRequests.Add(new ApprovalRequest { Id = 10, ReferenceNo = "APR-10", EaTaskId = 2010, RequestTitle = "Laptop", RequestedBy = "jay  PUJARI",
            WorkflowStatus = "Approved", RequiredApprovalDate = Ist(9, 18), CreatedBy = "ea", CreatedAt = Ist(9, 10) });
        db.ApprovalPhaseTats.Add(new ApprovalPhaseTat { ApprovalRequestId = 10, TaskType = "Actual", ReviewCycleNumber = 0,
            StartedAt = Ist(9, 15, 9), EndedAt = Ist(9, 16, 9), CreatedBy = "seed", CreatedDate = Ist(9, 15) });

        var meetingModule = new BusinessModule { Id = 50, Name = "Meeting", IsActive = true, CreatedBy = "seed", CreatedDate = Ist(9, 1) };
        db.BusinessModules.Add(meetingModule);
        db.Meetings.Add(new Meeting { Id = 20, MeetingNumber = "MTG-20", Title = "Board", StartDateTime = Ist(9, 17),
            DoerIds = [Emp, "S5I-2"], DoerNames = [EmpName, "Other Doer"], CreatedBy = "seed", CreatedDate = Ist(9, 1) });
        db.Tasks.Add(new EaTask { Id = 3020, BusinessModuleId = 50, ModuleName = "Meeting", BusinessRecordId = "20", Task = "Board",
            ExecutionStatus = EaTaskExecutionStatus.Completed, AllottedTatMinutes = 60, TatUsedMinutes = 90,
            StartedAt = Ist(9, 17, 10), CompletedAt = Ist(9, 17, 12), CreatedBy = "seed", CreatedDate = Ist(9, 1) });

        db.Tasks.AddRange(
            new EaTask { Id = 4030, BusinessModuleId = 9, ModuleName = "Follow-up", BusinessRecordId = "30", Task = "Chase", ExecutionStatus = EaTaskExecutionStatus.NotStarted, CreatedBy = "seed", CreatedDate = Ist(9, 1) },
            new EaTask { Id = 4031, BusinessModuleId = 9, ModuleName = "Follow-up", BusinessRecordId = "31", Task = "Sunday", ExecutionStatus = EaTaskExecutionStatus.NotStarted, CreatedBy = "seed", CreatedDate = Ist(9, 1) },
            new EaTask { Id = 5040, BusinessModuleId = 7, ModuleName = "Travel & Hospitality", BusinessRecordId = "40", Task = "Trip", ExecutionStatus = EaTaskExecutionStatus.Completed,
                StartedAt = Ist(9, 14), CompletedAt = Ist(9, 15), CreatedBy = "seed", CreatedDate = Ist(9, 1) });
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
    public async Task Kpis_Matrix_CountsEveryModule_ByTaskType()
    {
        await using var db = await SeedAsync();

        var r = await new EmEmployeeReportService(db).GetKpisAsync(Week38, default);

        Assert.Equal((new DateTime(2026, 9, 14), new DateTime(2026, 9, 19)), (r.WeekStart, r.WeekEnd)); // Mon–Sat
        var s = r.Matrix.Summary;
        Assert.Equal((8, 5, 3, 3, 2, 3, 0), (s.Planned, s.Completed, s.NotCompleted, s.OnTime, s.Delayed, s.Overdue, s.Pending));
        Assert.Equal(37.5m, s.NotCompletedPct);
        Assert.Equal(40m, s.DelayedPct);
        Assert.Equal((5, 3, 3, 0, 2), (r.Matrix.Actual.Planned, r.Matrix.Actual.Completed, r.Matrix.Actual.OnTime, r.Matrix.Actual.Delayed, r.Matrix.Actual.Overdue));
        Assert.Equal((1, 1, 1), (r.Matrix.Review.Planned, r.Matrix.Review.Completed, r.Matrix.Review.Delayed));
        Assert.Equal((1, 0, 1), (r.Matrix.Rework.Planned, r.Matrix.Rework.Completed, r.Matrix.Rework.Overdue));
        Assert.Equal((1, 1, 1), (r.Matrix.Meeting.Planned, r.Matrix.Meeting.Completed, r.Matrix.Meeting.Delayed));
    }

    [Fact]
    public async Task Kpis_Modules_TaskStatus_Tat_Deltas_AndFocusAreas()
    {
        await using var db = await SeedAsync();

        var r = await new EmEmployeeReportService(db).GetKpisAsync(Week38, default);

        var byModule = r.Modules.ToDictionary(m => m.Module, m => m.Kpi.Planned);
        Assert.Equal((4, 1, 1, 1, 1), (byModule["Delegation"], byModule["Approval"], byModule["Meeting"], byModule["Follow-up"], byModule["Travel"]));

        Assert.Equal((8, 3, 0, 2, 2, 1), (r.TaskStatus.Total, r.TaskStatus.OnTime, r.TaskStatus.Pending, r.TaskStatus.Delayed, r.TaskStatus.Overdue, r.TaskStatus.Rework));

        Assert.Equal(4, r.Tat.MeasuredItems);           // D1 Actual, Review, Rework (live) and the Meeting
        Assert.True(r.Tat.IsOver);
        Assert.Equal(r.Tat.AllottedMinutes - r.Tat.UsedMinutes, r.Tat.DifferenceMinutes);

        // Week 37 had one on-time completion (D4).
        Assert.Equal((7, 4, 2, 2), (r.Matrix.Summary.Delta.Planned, r.Matrix.Summary.Delta.Completed, r.Matrix.Summary.Delta.OnTime, r.Matrix.Summary.Delta.Delayed));

        Assert.Contains("Reduce delays (2 delayed)", r.FocusAreas.Improve);
        Assert.Contains("Clear overdue work (3 overdue)", r.FocusAreas.Improve);
        Assert.Contains("Reduce rework (1 rework tasks)", r.FocusAreas.Improve);
        Assert.Contains("TAT used is over the allotted limit", r.FocusAreas.Improve);
    }

    [Fact]
    public async Task WorkItems_ListsEveryModuleInOneShape_AndFilters()
    {
        await using var db = await SeedAsync();
        var svc = new EmEmployeeReportService(db);

        var all = await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, EmployeeName = EmpName, Year = 2026, Week = 38 }, default);
        Assert.Equal(8, all.TotalCount);
        Assert.Equal(new[] { "Approval", "Delegation", "Follow-up", "Meeting", "Travel" }, all.Items.Select(i => i.Module).Distinct().OrderBy(x => x));

        var overdue = await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, EmployeeName = EmpName, Year = 2026, Week = 38, Performance = "overdue" }, default);
        Assert.Equal(new[] { "Delegation:1:Rework:1", "Delegation:3:Actual:0", "Follow-up:30:Actual:0" }, overdue.Items.Select(i => i.ItemKey).OrderBy(x => x));

        var rework = Assert.Single((await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, Year = 2026, Week = 38, TaskType = "Rework" }, default)).Items);
        Assert.Equal((1, "InProgress", 10), (rework.ReviewCycleNumber, rework.Status, rework.AllottedTatMinutes!.Value));
        Assert.True(rework.TatDifferenceMinutes < 0);

        var meeting = Assert.Single((await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, Year = 2026, Week = 38, Module = "Meeting" }, default)).Items);
        Assert.Equal((Emp, EmpName, -30), (meeting.OwnerId, meeting.OwnerName, meeting.TatDifferenceMinutes!.Value));

        // AllWeeks brings in the Week 37 item; the Sunday item is never in a week.
        var everything = await svc.GetWorkItemsAsync(new EmWorkItemQueryDto { EmployeeId = Emp, EmployeeName = EmpName, AllWeeks = true, PageSize = 200 }, default);
        Assert.Contains(everything.Items, i => i.ItemKey == "Delegation:4:Actual:0" && i.Week == 37);
        Assert.Contains(everything.Items, i => i.ItemKey == "Follow-up:31:Actual:0" && i.Week == null);
    }

    [Fact]
    public async Task ApprovalAndTravel_AreMatchedByName_OtherDoersAreExcluded_AndTeamViewIncludesEveryone()
    {
        await using var db = await SeedAsync();
        var svc = new EmEmployeeReportService(db);

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

        var r = await new EmEmployeeReportService(db).GetTrendsAsync(new EmEmployeeTrendQueryDto { EmployeeId = Emp, EmployeeName = EmpName, Year = 2026, Week = 38, Weeks = 3 }, default);

        Assert.Equal(new[] { "W36", "W37", "W38" }, r.Weeks.Select(w => w.Label));
        Assert.Equal((1, 1), (r.Weeks[1].Summary.Completed, r.Weeks[1].Actual.OnTime));
        Assert.Equal((8, 5, 3, 2), (r.Weeks[2].Summary.Planned, r.Weeks[2].Summary.Completed, r.Weeks[2].Summary.OnTime, r.Weeks[2].Summary.Delayed));
    }

    [Theory]
    [InlineData(2026, 54)]
    [InlineData(1999, 10)]
    public async Task InvalidWeek_Is400(int year, int week)
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<BadRequestException>(() => new EmEmployeeReportService(db).GetKpisAsync(new EmEmployeeReportQueryDto { Year = year, Week = week }, default));
    }

    [Fact]
    public async Task InvalidFilters_Are400()
    {
        await using var db = NewDb();
        var svc = new EmEmployeeReportService(db);
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetWorkItemsAsync(new EmWorkItemQueryDto { Performance = "Late" }, default));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetWorkItemsAsync(new EmWorkItemQueryDto { PageSize = 500 }, default));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetKpisAsync(new EmEmployeeReportQueryDto { Year = 2026 }, default));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetTrendsAsync(new EmEmployeeTrendQueryDto { Weeks = 13 }, default));
    }
}
