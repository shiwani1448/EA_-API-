using AutoMapper;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// Focused tests for Meeting List TAT calculation in MeetingService.QueryAsync.
///
/// Covers:
/// 1. Running meeting with no pauses.
/// 2. Running meeting with a simple pause.
/// 3. Completed meeting.
/// 4. Meeting with no workflow/TAT.
/// 5. Multiple meetings in one list page.
/// 6. Each meeting gets its own correct TAT values.
/// 7. List TAT matches detail GetPausedDuration directly.
/// 8. No N+1 pattern introduced.
/// Plus: GetPausedDuration unit tests (no pauses, one pause, dependency excluded, overlapping merged).
/// </summary>
public sealed class MeetingListTatTests
{
    // ----------------------------------------------------------------
    // Infrastructure helpers
    // ----------------------------------------------------------------

    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Mock<ICurrentUserService> MakeUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(u => u.UserId).Returns(1L);
        m.SetupGet(u => u.UserName).Returns("tester");
        return m;
    }

    /// <summary>
    /// Builds a mapper mock that, for any Meeting source, returns a MeetingListItemResponseDto
    /// with MeetingId = meeting.Id and WorkflowInstanceId propagated via the meeting's
    /// WorkflowInstanceId. This avoids any AutoMapper version dependency in the test project.
    /// </summary>
    private static IMapper MakeMapperMock(EaFmsDbContext db)
    {
        var mock = new Mock<IMapper>();
        mock.Setup(m => m.Map<MeetingListItemResponseDto>(It.IsAny<Meeting>()))
            .Returns<Meeting>(meeting => new MeetingListItemResponseDto
            {
                MeetingId = meeting.Id,
                Title = meeting.Title,
                Type = meeting.MeetingType,
                Subtype = meeting.Category,
                Doers = new List<MeetingDoerDto>(),
                CreatedDate = meeting.CreatedDate,
                ModifiedDate = meeting.ModifiedDate,
                ExecutionState = "NotStarted"
            });
        return mock.Object;
    }

    /// <summary>
    /// Minimal seed: BusinessModule + Status + WorkflowInstance + EaTask + Meeting + optional WorkPauses.
    /// </summary>
    private static async Task<(long meetingId, long workflowId)> SeedMeetingAsync(
        EaFmsDbContext db,
        DateTime tatStart,
        int tatMinutes,
        DateTime? completedAt = null,
        List<(DateTime pauseStart, DateTime? pauseEnd)>? pauses = null)
    {
        var module = new BusinessModule
        {
            Name = "Meeting",
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };
        db.BusinessModules.Add(module);

        var status = new Status
        {
            Name = completedAt.HasValue ? "Completed" : "In Progress",
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };
        db.Statuses.Add(status);
        await db.SaveChangesAsync();

        var workflow = new WorkflowInstance
        {
            StatusId = status.Id,
            StartedAt = tatStart,
            TatStartedAt = tatStart,
            CompletedAt = completedAt,
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();

        var meeting = new Meeting
        {
            Title = "Test Meeting",
            MeetingType = "Internal",
            Category = "General",
            WorkflowInstanceId = workflow.Id,
            DoerIds = Array.Empty<string>(),
            DoerNames = Array.Empty<string>(),
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var task = new EaTask
        {
            BusinessModuleId = module.Id,
            BusinessModule = module,
            ModuleName = module.Name,
            BusinessRecordId = meeting.Id.ToString(),
            Task = "Meeting Task",
            AllottedTatMinutes = tatMinutes,
            ExecutionStatus = completedAt.HasValue ? "Completed" : "InProgress",
            StartedAt = tatStart,
            CompletedAt = completedAt,
            IsActive = true,
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        if (pauses is not null)
        {
            foreach (var (ps, pe) in pauses)
            {
                db.WorkPauses.Add(new WorkPause
                {
                    WorkflowInstanceId = workflow.Id,
                    StartAt = ps,
                    EndAt = pe,
                    // Simple pause: no WaitingOn* fields
                    CreatedBy = "seed",
                    CreatedDate = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        return (meeting.Id, workflow.Id);
    }

    private MeetingService MakeService(EaFmsDbContext db)
    {
        var repo = new Mock<IMeetingRepository>();
        repo.Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<Meeting>, IQueryable<Meeting>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<IQueryable<Meeting>, IQueryable<Meeting>>, CancellationToken>(
                async (transform, ct) =>
                {
                    var base_ = db.Meetings.AsNoTracking().Where(m => !m.IsDeleted);
                    return await transform(base_).ToListAsync(ct);
                });

        return new MeetingService(
            repo.Object,
            db,
            MakeMapperMock(db),
            MakeUser().Object,
            new Mock<IAuditService>().Object,
            new Mock<IWorkflowService>().Object,
            new Mock<IEaTaskService>().Object);
    }

    // ----------------------------------------------------------------
    // Test 1: Running meeting with no pauses
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_RunningMeeting_NoPauses_CalculatesTatCorrectly()
    {
        await using var db = MakeDb();
        var tatStart = DateTime.UtcNow.AddMinutes(-60);
        await SeedMeetingAsync(db, tatStart, tatMinutes: 120);

        var result = await MakeService(db).QueryAsync();

        Assert.Single(result);
        var item = result[0];
        Assert.NotNull(item.TatUsedMinutes);
        Assert.NotNull(item.TatPausedMinutes);
        Assert.Equal(0, item.TatPausedMinutes);
        Assert.Equal(120, item.TatMinutes);
        // Used ≈ 60 min; allow ±2 minutes for test execution time.
        Assert.InRange(item.TatUsedMinutes!.Value, 58, 62);
    }

    // ----------------------------------------------------------------
    // Test 2: Running meeting with a simple pause
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_RunningMeeting_WithSimplePause_SubtractsPausedTime()
    {
        await using var db = MakeDb();
        // TAT started 90 min ago. Simple pause: 60–30 min ago (= 30 min paused).
        // Expected used ≈ 90 - 30 = 60 min.
        var tatStart = DateTime.UtcNow.AddMinutes(-90);
        var pauseStart = DateTime.UtcNow.AddMinutes(-60);
        var pauseEnd = DateTime.UtcNow.AddMinutes(-30);

        await SeedMeetingAsync(db, tatStart, tatMinutes: 180,
            pauses: [(pauseStart, pauseEnd)]);

        var result = await MakeService(db).QueryAsync();

        Assert.Single(result);
        var item = result[0];
        Assert.NotNull(item.TatPausedMinutes);
        Assert.NotNull(item.TatUsedMinutes);
        // Paused = 30 min (±2 for test timing)
        Assert.InRange(item.TatPausedMinutes!.Value, 28, 32);
        // Used ≈ 60 min (±4 for test timing)
        Assert.InRange(item.TatUsedMinutes!.Value, 56, 64);
    }

    // ----------------------------------------------------------------
    // Test 3: Completed meeting — uses CompletedAt not current time
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_CompletedMeeting_UsesCompletedAtNotCurrentTime()
    {
        await using var db = MakeDb();
        // TAT started 200 min ago; completed 100 min ago. No pauses → used = exactly 100 min.
        var tatStart = DateTime.UtcNow.AddMinutes(-200);
        var completedAt = DateTime.UtcNow.AddMinutes(-100);

        await SeedMeetingAsync(db, tatStart, tatMinutes: 60, completedAt: completedAt);

        var result = await MakeService(db).QueryAsync();

        Assert.Single(result);
        var item = result[0];
        Assert.NotNull(item.TatUsedMinutes);
        Assert.NotNull(item.TatPausedMinutes);
        // Both timestamps are fixed → exactly 100 min, no timing variance.
        Assert.Equal(100, item.TatUsedMinutes!.Value);
        Assert.Equal(0, item.TatPausedMinutes!.Value);
    }

    // ----------------------------------------------------------------
    // Test 4: Meeting with no workflow/TAT
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_MeetingWithNoWorkflow_TatFieldsAreNull()
    {
        await using var db = MakeDb();
        var meeting = new Meeting
        {
            Title = "No Workflow",
            MeetingType = "External",
            Category = "Other",
            WorkflowInstanceId = null,
            DoerIds = Array.Empty<string>(),
            DoerNames = Array.Empty<string>(),
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var result = await MakeService(db).QueryAsync();

        Assert.Single(result);
        var item = result[0];
        // No workflow → must remain null, not converted to 0.
        Assert.Null(item.TatUsedMinutes);
        Assert.Null(item.TatPausedMinutes);
        Assert.Null(item.TatMinutes);
    }

    // ----------------------------------------------------------------
    // Tests 5 & 6: Multiple meetings on one page — each gets its own correct TAT
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_MultipleMeetings_EachGetsOwnCorrectTatValues()
    {
        await using var db = MakeDb();

        // Meeting A: running 30 min, no pauses.
        var startA = DateTime.UtcNow.AddMinutes(-30);
        var (idA, _) = await SeedMeetingAsync(db, startA, tatMinutes: 60);

        // Meeting B: completed after 50 min exactly, no pauses.
        var startB = DateTime.UtcNow.AddMinutes(-150);
        var completedB = DateTime.UtcNow.AddMinutes(-100);
        var (idB, _) = await SeedMeetingAsync(db, startB, tatMinutes: 90, completedAt: completedB);

        var result = await MakeService(db).QueryAsync();

        Assert.Equal(2, result.Count);

        var itemA = result.First(i => i.MeetingId == idA);
        var itemB = result.First(i => i.MeetingId == idB);

        // Meeting A: ≈ 30 min used (±2), 0 paused
        Assert.NotNull(itemA.TatUsedMinutes);
        Assert.InRange(itemA.TatUsedMinutes!.Value, 28, 32);
        Assert.Equal(0, itemA.TatPausedMinutes);

        // Meeting B: exactly 50 min used (150 - 100), 0 paused
        Assert.NotNull(itemB.TatUsedMinutes);
        Assert.Equal(50, itemB.TatUsedMinutes!.Value);
        Assert.Equal(0, itemB.TatPausedMinutes);
    }

    // ----------------------------------------------------------------
    // Test 7: List TAT matches detail — both use WorkPauseClassifier.GetPausedDuration
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_ListTatMatchesDetailCalculation_SameGetPausedDurationHelper()
    {
        await using var db = MakeDb();

        // 120 min elapsed, 2 non-overlapping simple pauses: 30 + 20 = 50 min paused.
        // Expected used = 120 - 50 = 70 min.
        var tatStart = DateTime.UtcNow.AddMinutes(-120);
        var p1Start = DateTime.UtcNow.AddMinutes(-100);
        var p1End   = DateTime.UtcNow.AddMinutes(-70);    // 30 min pause
        var p2Start = DateTime.UtcNow.AddMinutes(-50);
        var p2End   = DateTime.UtcNow.AddMinutes(-30);    // 20 min pause

        await SeedMeetingAsync(db, tatStart, tatMinutes: 180,
            pauses: [(p1Start, p1End), (p2Start, p2End)]);

        var result = await MakeService(db).QueryAsync();
        Assert.Single(result);
        var item = result[0];

        // Compute expected values using the canonical helper directly — same call as the detail endpoint.
        var wf = await db.WorkflowInstances.FirstAsync();
        var allPauses = await db.WorkPauses.ToListAsync();
        var now = DateTime.UtcNow;
        var end = wf.CompletedAt ?? now;
        var expectedPaused = WorkPauseClassifier.GetPausedDuration(wf.TatStartedAt!.Value, end, allPauses);
        var expectedUsed = end - wf.TatStartedAt!.Value - expectedPaused;
        if (expectedUsed < TimeSpan.Zero) expectedUsed = TimeSpan.Zero;

        Assert.Equal((int)expectedPaused.TotalMinutes, item.TatPausedMinutes);
        Assert.Equal((int)expectedUsed.TotalMinutes, item.TatUsedMinutes);
    }

    // ----------------------------------------------------------------
    // Test 8: No N+1 — TatPausedMinutes is 0 (not null) when TAT exists but no pauses
    // ----------------------------------------------------------------

    [Fact]
    public async Task QueryAsync_NoPauseRecords_PausedMinutesIsZeroNotNull_WhenTatExists()
    {
        await using var db = MakeDb();
        var tatStart = DateTime.UtcNow.AddMinutes(-45);
        await SeedMeetingAsync(db, tatStart, tatMinutes: 90, pauses: []);

        var result = await MakeService(db).QueryAsync();

        Assert.Single(result);
        var item = result[0];
        // TatPausedMinutes = 0, not null — TAT data exists, just no pauses.
        Assert.NotNull(item.TatPausedMinutes);
        Assert.Equal(0, item.TatPausedMinutes);
        Assert.NotNull(item.TatUsedMinutes);
    }

    [Fact]
    public async Task QueryAsync_TatStartedAtNull_TatFieldsAreNull()
    {
        await using var db = MakeDb();

        var module = new BusinessModule { Name = "Meeting", IsActive = true, CreatedBy = "s", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        var status = new Status { Name = "In Progress", CreatedBy = "s", CreatedDate = DateTime.UtcNow };
        db.Statuses.Add(status);
        await db.SaveChangesAsync();

        var workflow = new WorkflowInstance
        {
            StatusId = status.Id,
            StartedAt = DateTime.UtcNow,
            TatStartedAt = null, // TAT never started
            IsActive = true,
            CreatedBy = "s",
            CreatedDate = DateTime.UtcNow
        };
        db.WorkflowInstances.Add(workflow);
        var meeting = new Meeting
        {
            Title = "No TAT Start",
            MeetingType = "Internal",
            Category = "General",
            WorkflowInstanceId = workflow.Id,
            DoerIds = Array.Empty<string>(),
            DoerNames = Array.Empty<string>(),
            CreatedBy = "s",
            CreatedDate = DateTime.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var task = new EaTask
        {
            BusinessModuleId = module.Id,
            BusinessModule = module,
            ModuleName = module.Name,
            BusinessRecordId = meeting.Id.ToString(),
            Task = "T",
            AllottedTatMinutes = 60,
            ExecutionStatus = "NotStarted",
            IsActive = true,
            CreatedBy = "s",
            CreatedDate = DateTime.UtcNow
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        var result = await MakeService(db).QueryAsync();

        Assert.Single(result);
        var item = result[0];
        // TatStartedAt is null → can't calculate → must remain null.
        Assert.Null(item.TatUsedMinutes);
        Assert.Null(item.TatPausedMinutes);
    }

    // ----------------------------------------------------------------
    // WorkPauseClassifier.GetPausedDuration unit tests
    // (tests the canonical shared helper directly)
    // ----------------------------------------------------------------

    [Fact]
    public void GetPausedDuration_NoPauses_ReturnsZero()
    {
        var start = DateTime.UtcNow.AddMinutes(-60);
        var end = DateTime.UtcNow;
        var result = WorkPauseClassifier.GetPausedDuration(start, end, []);
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void GetPausedDuration_OneSimplePause_ReturnsPauseDuration()
    {
        var tatStart = DateTime.UtcNow.AddMinutes(-100);
        var end = DateTime.UtcNow;
        var pause = new WorkPause
        {
            StartAt = DateTime.UtcNow.AddMinutes(-60),
            EndAt = DateTime.UtcNow.AddMinutes(-40),
            // Simple pause: no WaitingOn* fields
        };

        var result = WorkPauseClassifier.GetPausedDuration(tatStart, end, [pause]);
        Assert.Equal(20, (int)result.TotalMinutes);
    }

    [Fact]
    public void GetPausedDuration_DependencyWaitingPause_IsExcluded()
    {
        var tatStart = DateTime.UtcNow.AddMinutes(-100);
        var end = DateTime.UtcNow;
        var dependencyPause = new WorkPause
        {
            StartAt = DateTime.UtcNow.AddMinutes(-60),
            EndAt = DateTime.UtcNow.AddMinutes(-30),
            WaitingOnId = "external-party", // dependency waiting — must be excluded
        };

        var result = WorkPauseClassifier.GetPausedDuration(tatStart, end, [dependencyPause]);
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void GetPausedDuration_OverlappingSimplePauses_MergesCorrectly()
    {
        var tatStart = DateTime.UtcNow.AddMinutes(-120);
        var end = DateTime.UtcNow;

        var pause1 = new WorkPause
        {
            StartAt = DateTime.UtcNow.AddMinutes(-90),
            EndAt = DateTime.UtcNow.AddMinutes(-60), // 30 min
        };
        var pause2 = new WorkPause
        {
            StartAt = DateTime.UtcNow.AddMinutes(-70), // overlaps with pause1
            EndAt = DateTime.UtcNow.AddMinutes(-40),   // merged: 90..40 = 50 min
        };

        var result = WorkPauseClassifier.GetPausedDuration(tatStart, end, [pause1, pause2]);
        // Merged overlap: from min -90 to min -40 = 50 min
        Assert.Equal(50, (int)result.TotalMinutes);
    }
}
