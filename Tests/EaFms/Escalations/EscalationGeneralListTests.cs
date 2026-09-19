using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Mapping;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Escalations;

public class EscalationGeneralListTests
{
    private static EaFmsDbContext MakeDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static readonly IMapper Mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static EscalationService Service(EaFmsDbContext db) => new(new EscalationRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(u => u.UserId == 7 && u.UserName == "EA User"), Mock.Of<IAuditService>());

    [Fact]
    public async Task GeneralList_IsPagedOrderedAndBatchesFollowupTaskModuleAndLevelContext()
    {
        await using var db = MakeDb();
        var now = DateTime.UtcNow;
        var module = new BusinessModule { Name = "Meeting", IsActive = true, CreatedBy = "seed", CreatedDate = now };
        var level = new EscalationLevel { Code = "L1", Name = "Department Head", Level = 1, CreatedBy = "seed", CreatedDate = now };
        db.AddRange(module, level); await db.SaveChangesAsync();
        var completedFollowup = new Followup { BusinessModuleId = module.Id, BusinessRecordId = "completed", ReminderAt = now.AddDays(1), Note = "Completed parent reminder", ReminderRecipientEmployeeId = "EMP-1", ReminderRecipientName = "Completed Recipient", DueAt = now.AddDays(2), CreatedBy = "seed", CreatedDate = now };
        var pausedFollowup = new Followup { BusinessModuleId = module.Id, BusinessRecordId = "paused", ReminderAt = now.AddDays(2), Note = "Paused parent reminder", ReminderRecipientEmployeeId = "EMP-2", ReminderRecipientName = "Paused Recipient", DueAt = now.AddDays(3), CreatedBy = "seed", CreatedDate = now };
        var workflow = new WorkflowInstance { BusinessModuleId = module.Id, BusinessRecordId = "paused", StartedAt = now, CreatedBy = "seed", CreatedDate = now };
        db.AddRange(completedFollowup, pausedFollowup, workflow); await db.SaveChangesAsync();
        db.Tasks.AddRange(
            new EaTask { BusinessModuleId = module.Id, BusinessRecordId = "completed", ModuleName = module.Name, Task = "Completed task", ExecutionStatus = "Completed", CreatedBy = "seed", CreatedDate = now },
            new EaTask { BusinessModuleId = module.Id, BusinessRecordId = "paused", ModuleName = module.Name, Task = "Paused task", ExecutionStatus = "InProgress", WorkflowInstanceId = workflow.Id, CreatedBy = "seed", CreatedDate = now });
        db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = workflow.Id, StartAt = now, CreatedBy = "seed", CreatedDate = now });
        await db.SaveChangesAsync();
        var first = new Escalation { FollowupId = completedFollowup.Id, EscalationLevelId = level.Id, InitiatedAt = now.AddMinutes(1), CreatedBy = "seed", CreatedDate = now };
        var second = new Escalation { FollowupId = pausedFollowup.Id, EscalationLevelId = level.Id, InitiatedAt = now.AddMinutes(2), AcknowledgedAt = now.AddMinutes(3), CreatedBy = "seed", CreatedDate = now };
        var third = new Escalation { FollowupId = pausedFollowup.Id, EscalationLevelId = level.Id, InitiatedAt = now.AddMinutes(4), ResolvedAt = now.AddMinutes(5), CreatedBy = "seed", CreatedDate = now };
        db.Escalations.AddRange(first, second, third); await db.SaveChangesAsync();

        var pageOne = await Service(db).GetPagedAsync(new EscalationListQueryDto { Page = 1, PageSize = 2 });
        var pageTwo = await Service(db).GetPagedAsync(new EscalationListQueryDto { Page = 2, PageSize = 2 });

        Assert.Equal((3, 2, 1), (pageOne.TotalCount, pageOne.Items.Count, pageOne.PageNumber));
        Assert.Equal(new[] { third.Id, second.Id }, pageOne.Items.Select(x => x.Id));
        Assert.Single(pageTwo.Items);
        Assert.Equal(first.Id, pageTwo.Items[0].Id);
        Assert.Equal(3, pageOne.Items.Concat(pageTwo.Items).Select(x => x.Id).Distinct().Count());
        var paused = pageOne.Items[0];
        Assert.Equal((level.Id, "Department Head", "Meeting", "Paused task", pausedFollowup.ReminderAt, "Paused parent reminder", "EMP-2", "Paused Recipient"),
            (paused.EscalationLevelId, paused.EscalationLevelName, paused.ModuleName, paused.Task, paused.ReminderAt, paused.Remark, paused.ReminderRecipientEmployeeId, paused.ReminderRecipientName));
        Assert.True(paused.IsResolved);
        Assert.NotNull(paused.ResolvedAt);
        Assert.Contains(pageTwo.Items, x => x.Task == "Completed task"); // completed parent does not hide escalation
    }

    [Fact]
    public async Task FollowupScopedList_RemainsSupported()
    {
        await using var db = MakeDb();
        var now = DateTime.UtcNow;
        var level = new EscalationLevel { Code = "L1", Name = "Level", Level = 1, CreatedBy = "seed", CreatedDate = now };
        var followup = new Followup { DueAt = now.AddDays(1), CreatedBy = "seed", CreatedDate = now };
        db.AddRange(level, followup); await db.SaveChangesAsync();
        db.Escalations.AddRange(
            new Escalation { FollowupId = followup.Id, EscalationLevelId = level.Id, InitiatedAt = now, CreatedBy = "seed", CreatedDate = now },
            new Escalation { FollowupId = followup.Id, EscalationLevelId = level.Id, InitiatedAt = now.AddMinutes(1), CreatedBy = "seed", CreatedDate = now });
        await db.SaveChangesAsync();

        var scoped = await Service(db).GetByFollowupIdAsync(followup.Id);
        Assert.Equal(2, scoped.Count);
        Assert.All(scoped, escalation => Assert.Equal(followup.Id, escalation.FollowupId));
    }
}