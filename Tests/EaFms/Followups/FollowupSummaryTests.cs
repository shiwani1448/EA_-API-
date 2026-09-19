using System;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

public class FollowupSummaryTests
{
    private static FollowupService Service(EaFmsDbContext db) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(x => x.UserId == 7 && x.UserName == "EA User"),
        Mock.Of<IAuditService>(), new FollowupSourceResolver(db));

    [Fact]
    public async Task Summary_CountsFollowupsOnly_ExcludesDeletedAndCompletedOperationalBuckets()
    {
        await using var db = MakeDb();
        var now = Clock.UtcNowTz;
        var today = IndiaBusinessCalendar.Today;
        var moduleOne = 101L;
        var moduleTwo = 202L;
        var dueToday = new Followup { BusinessModuleId = moduleOne, Subject = "Due today", DueAt = today.AddHours(23), CreatedBy = "seed", CreatedDate = now };
        var overdue = new Followup { BusinessModuleId = moduleOne, Subject = "Overdue", DueAt = now.AddDays(-1), CreatedBy = "seed", CreatedDate = now };
        var upcoming = new Followup { BusinessModuleId = moduleOne, Subject = "Upcoming", DueAt = now.AddDays(4), ReminderAt = now.AddDays(2), CreatedBy = "seed", CreatedDate = now };
        var completed = new Followup { BusinessModuleId = moduleOne, Subject = "Completed", DueAt = now.AddDays(-3), ReminderAt = now.AddDays(1), CompletedAt = now, CreatedBy = "seed", CreatedDate = now };
        var otherScope = new Followup { BusinessModuleId = moduleTwo, Subject = "Other", DueAt = now.AddDays(4), CreatedBy = "seed", CreatedDate = now };
        var deleted = new Followup { BusinessModuleId = moduleOne, Subject = "Deleted", DueAt = now.AddDays(-3), IsDeleted = true, CreatedBy = "seed", CreatedDate = now };
        db.Followups.AddRange(dueToday, overdue, upcoming, completed, otherScope, deleted);
        await db.SaveChangesAsync();
        db.Escalations.Add(new Escalation { FollowupId = upcoming.Id, EscalationLevelId = 1, InitiatedAt = now, CreatedBy = "seed", CreatedDate = now });
        await db.SaveChangesAsync();

        var summary = await Service(db).GetSummaryAsync(new FollowupListQueryDto());

        Assert.Equal(5, summary.Total);
        Assert.Equal(4, summary.Pending);
        Assert.Equal(1, summary.Completed);
        Assert.Equal(1, summary.DueToday);
        Assert.Equal(1, summary.Overdue);
        Assert.Equal(1, summary.UpcomingReminders);
        Assert.Equal(1, summary.Escalated);
    }

    [Fact]
    public async Task Summary_ReusesExistingFilters_AndDoesNotUseParentTaskStatusOrPause()
    {
        await using var db = MakeDb();
        var now = Clock.UtcNowTz;
        var included = new Followup { BusinessModuleId = 10, BusinessRecordId = "record-1", Subject = "Included", DueAt = now.AddDays(2), ReminderSendWhatsApp = true, ReminderRecipientEmployeeId = "EMP-1", CreatedBy = "seed", CreatedDate = now };
        var excluded = new Followup { BusinessModuleId = 20, BusinessRecordId = "record-2", Subject = "Excluded", DueAt = now.AddDays(2), ReminderSendWhatsApp = false, ReminderRecipientEmployeeId = "EMP-2", CreatedBy = "seed", CreatedDate = now };
        db.Followups.AddRange(included, excluded);
        var pausedWorkflow = new WorkflowInstance { BusinessModuleId = 20, BusinessRecordId = "record-2", StartedAt = now, CreatedBy = "seed", CreatedDate = now };
        db.WorkflowInstances.Add(pausedWorkflow);
        await db.SaveChangesAsync();
        db.Tasks.AddRange(
            new EaTask { BusinessModuleId = 10, BusinessRecordId = "record-1", ModuleName = "Module", Task = "Completed parent", ExecutionStatus = "Completed", CreatedBy = "seed", CreatedDate = now },
            new EaTask { BusinessModuleId = 20, BusinessRecordId = "record-2", ModuleName = "Module", Task = "Paused parent", ExecutionStatus = "InProgress", WorkflowInstanceId = pausedWorkflow.Id, CreatedBy = "seed", CreatedDate = now });
        db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = pausedWorkflow.Id, StartAt = now, CreatedBy = "seed", CreatedDate = now });
        await db.SaveChangesAsync();

        var all = await Service(db).GetSummaryAsync(new FollowupListQueryDto());
        var summary = await Service(db).GetSummaryAsync(new FollowupListQueryDto
        {
            BusinessModuleId = 10, ReminderRecipientEmployeeId = "EMP-1", ReminderSendWhatsApp = true
        });

        Assert.Equal((2, 2), (all.Total, all.Pending)); // Completed and paused parent tasks are informational only.
        Assert.Equal(1, summary.Total);
        Assert.Equal(1, summary.Pending);
        Assert.Equal(0, summary.Completed);
    }
}
