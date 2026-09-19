using System;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

public class FollowupEmployeeRecipientTests
{
    private static FollowupService Service(EaFmsDbContext db) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(x => x.UserId == 7 && x.UserName == "EA User"),
        Mock.Of<IAuditService>(), new FollowupSourceResolver(db));

    [Fact]
    public async Task EmployeeWhatsAppSnapshot_IsStored_Returned_AndProducesHandoffWithoutLocalUser()
    {
        var seed = await SeedAsync(); await using var _ = seed.Db;
        var module = seed.Modules["Meeting"];
        seed.Db.Tasks.Add(new Jarvis5.Entities.EaFms.EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = seed.Meeting.Id.ToString(), Task = "Prepare MOM", ExecutionStatus = "InProgress", CreatedBy = "seed", CreatedDate = Base });
        await seed.Db.SaveChangesAsync();

        var created = await Service(seed.Db).CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), DueAt = Base.AddDays(2),
            ReminderAt = Base.AddDays(1), ReminderSendWhatsApp = true,
            ReminderRecipientEmployeeId = "SSI-1096", ReminderRecipientName = "Nuha Khan Fathima",
            ReminderWhatsAppNumber = " 8369543637 ", ReminderRecipientEmail = "nuha@example.com",
            Remark = "Please complete the pending task."
        });

        Assert.Null(created.ReminderRecipientUserId);
        Assert.Equal(("SSI-1096", "Nuha Khan Fathima", "8369543637", "nuha@example.com"),
            (created.Recipient!.EmployeeId, created.Recipient.Name, created.Recipient.Phone, created.Recipient.Email));
        Assert.NotNull(created.WhatsApp);
        Assert.Equal(("Meeting", created.ReminderAt, "Please complete the pending task.", true, false),
            (created.ModuleName, created.ReminderAt, created.Remark, created.ReminderSendWhatsApp, created.ReminderSendEmail));
        Assert.Contains("Module: Meeting", created.WhatsApp!.Message);
        Assert.Contains($"Task ID: {created.EaTaskId}", created.WhatsApp.Message);
        Assert.Contains("Task: Prepare MOM", created.WhatsApp.Message);
        Assert.Contains($"Follow-up Date: {created.ReminderAt:O}", created.WhatsApp.Message);
        Assert.Contains("Remark: Please complete the pending task.", created.WhatsApp.Message);

        var single = await Service(seed.Db).GetByIdAsync(created.Id);
        var list = (await Service(seed.Db).GetPagedAsync(new FollowupListQueryDto { BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString() })).Items;
        Assert.Equal("SSI-1096", single.Recipient!.EmployeeId);
        Assert.Equal("8369543637", Assert.Single(list).Recipient!.Phone);
        Assert.Equal(created.ReminderAt, single.ReminderAt);
        Assert.Equal(created.Remark, single.Remark);
        Assert.Equal(created.EaTaskId, Assert.Single(list).EaTaskId);
    }

    [Fact]
    public async Task BlankRecipientName_UsesCleanGreeting_AndUpdateReplacesSnapshot()
    {
        await using var db = MakeDb();
        var service = Service(db);
        var created = await service.CreateAsync(new CreateFollowupRequestDto { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendWhatsApp = true, ReminderWhatsAppNumber = "9", ReminderRecipientEmployeeId = "E1" });
        Assert.StartsWith("Hello,", created.WhatsApp!.Message);
        Assert.DoesNotContain("Hello ,", created.WhatsApp.Message);

        var updated = await service.UpdateAsync(created.Id, new UpdateFollowupRequestDto { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendWhatsApp = true, ReminderRecipientEmployeeId = "E2", ReminderRecipientName = "Recipient", ReminderWhatsAppNumber = "8", ReminderRecipientEmail = "recipient@example.com" });
        Assert.Equal(("E2", "Recipient", "8", "recipient@example.com"), (updated.Recipient!.EmployeeId, updated.Recipient.Name, updated.Recipient.Phone, updated.Recipient.Email));
    }
}