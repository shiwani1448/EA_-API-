using System;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

public class FollowupWhatsAppActionTests
{
    private static FollowupService Service(EaFmsDbContext db) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(x => x.UserId == 7 && x.UserName == "EA User"),
        Mock.Of<IAuditService>(), new FollowupSourceResolver(db));

    [Fact]
    public async Task SendWhatsApp_ReturnsPersistedPhoneAndExistingMessage_ForCompletedTask()
    {
        var seed = await SeedAsync(); await using var _ = seed.Db;
        var module = seed.Modules["Meeting"];
        seed.Db.Tasks.Add(new EaTask { BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), ModuleName = module.Name, Task = "Prepare MOM", ExecutionStatus = "Completed", CreatedBy = "seed", CreatedDate = Base });
        await seed.Db.SaveChangesAsync();
        var service = Service(seed.Db);
        var followup = await service.CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1),
            ReminderSendWhatsApp = true, ReminderRecipientEmployeeId = "HRMS-101", ReminderRecipientName = "HRMS Recipient",
            ReminderWhatsAppNumber = " 8369543637 ", Remark = "Please complete the MOM."
        });

        var handoff = await service.SendWhatsAppAsync(followup.Id);

        Assert.Equal("8369543637", handoff.Phone);
        Assert.Contains("Hello HRMS Recipient,", handoff.Message);
        Assert.Contains("Module: Meeting", handoff.Message);
        Assert.Contains($"Task ID: {followup.EaTaskId}", handoff.Message);
        Assert.Contains("Task: Prepare MOM", handoff.Message);
        Assert.Contains($"Follow-up Date: {followup.ReminderAt:O}", handoff.Message);
        Assert.Contains("Remark: Please complete the MOM.", handoff.Message);
        Assert.Equal(handoff.Message, (await service.GetByIdAsync(followup.Id)).WhatsApp!.Message);
    }

    [Fact]
    public async Task SendWhatsApp_RejectsDisabledOrMissingPhone_AndDoesNotWrite()
    {
        await using var db = MakeDb();
        var service = Service(db);
        var disabled = await service.CreateAsync(new CreateFollowupRequestDto { DueAt = Base.AddDays(2) });
        await Assert.ThrowsAsync<BadRequestException>(() => service.SendWhatsAppAsync(disabled.Id));

        var missingPhone = new Followup { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendWhatsApp = true, CreatedBy = "seed", CreatedDate = Base };
        db.Followups.Add(missingPhone);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BadRequestException>(() => service.SendWhatsAppAsync(missingPhone.Id));
        Assert.Equal(2, await db.Followups.CountAsync());
    }

    [Fact]
    public async Task SendWhatsApp_AllowsPausedTaskAndMissingNameWithoutAnyDeliverySideEffect()
    {
        var seed = await SeedAsync(); await using var _ = seed.Db;
        var module = seed.Modules["Meeting"];
        var workflow = new WorkflowInstance { BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), CreatedBy = "seed", CreatedDate = Base };
        seed.Db.WorkflowInstances.Add(workflow); await seed.Db.SaveChangesAsync();
        seed.Db.Tasks.Add(new EaTask { BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), ModuleName = module.Name, Task = "Paused task", ExecutionStatus = "InProgress", WorkflowInstanceId = workflow.Id, CreatedBy = "seed", CreatedDate = Base });
        seed.Db.WorkPauses.Add(new WorkPause { WorkflowInstanceId = workflow.Id, StartAt = Base, CreatedBy = "seed", CreatedDate = Base });
        await seed.Db.SaveChangesAsync();
        var service = Service(seed.Db);
        var followup = await service.CreateAsync(new CreateFollowupRequestDto { BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendWhatsApp = true, ReminderWhatsAppNumber = "9" });
        var before = (await seed.Db.Escalations.CountAsync(), await seed.Db.Notifications.CountAsync());

        var handoff = await service.SendWhatsAppAsync(followup.Id);

        Assert.StartsWith("Hello,", handoff.Message);
        Assert.DoesNotContain("Hello ,", handoff.Message);
        Assert.Equal(before, (await seed.Db.Escalations.CountAsync(), await seed.Db.Notifications.CountAsync()));
    }
}