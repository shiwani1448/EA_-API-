using System;
using System.Threading;
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

public class FollowupEmailSnapshotTests
{
    private static FollowupService Service(EaFmsDbContext db, IEaReminderEmailSender sender) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(x => x.UserId == 7 && x.UserName == "EA User"),
        Mock.Of<IAuditService>(), new FollowupSourceResolver(db), sender);

    [Fact]
    public async Task HrmsSnapshot_EmailConfigurationAndSending_NeverUsesUsers()
    {
        var seed = await SeedAsync(); await using var _ = seed.Db;
        var module = seed.Modules["Meeting"];
        seed.Db.Tasks.Add(new EaTask { BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), ModuleName = module.Name, Task = "Prepare MOM", ExecutionStatus = "Completed", CreatedBy = "seed", CreatedDate = Base });
        await seed.Db.SaveChangesAsync();
        var sender = new Mock<IEaReminderEmailSender>();
        EaReminderEmailMessage? sent = null;
        sender.Setup(x => x.SendAsync(It.IsAny<EaReminderEmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EaReminderEmailMessage, CancellationToken>((message, _) => sent = message)
            .Returns(Task.CompletedTask);
        var service = Service(seed.Db, sender.Object);

        var created = await service.CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1),
            ReminderSendEmail = true, ReminderRecipientEmployeeId = "HRMS-101", ReminderRecipientName = "HRMS Recipient",
            ReminderRecipientEmail = "recipient@example.com", Remark = "Please complete the MOM."
        });
        Assert.Null(created.ReminderRecipientUserId);
        Assert.Equal(("HRMS-101", "HRMS Recipient", "recipient@example.com", true, false),
            (created.Recipient!.EmployeeId, created.Recipient.Name, created.Recipient.Email, created.ReminderSendEmail, created.ReminderSendWhatsApp));

        await service.SendEmailAsync(created.Id);

        Assert.NotNull(sent);
        Assert.Equal("recipient@example.com", sent!.To);
        Assert.Equal("Reminder / Follow-up - Prepare MOM", sent.Subject);
        Assert.Contains("Hello HRMS Recipient,", sent.Body);
        Assert.Contains("Module: Meeting", sent.Body);
        Assert.Contains($"Task ID: {created.EaTaskId}", sent.Body);
        Assert.Contains("Task: Prepare MOM", sent.Body);
        Assert.Contains($"Follow-up Date: {created.ReminderAt:O}", sent.Body);
        Assert.Contains("Remark: Please complete the MOM.", sent.Body);
    }

    [Fact]
    public async Task EmailSnapshot_IsRequiredAndValidated_AndUpdateNeedsNoUserId()
    {
        await using var db = MakeDb();
        var sender = Mock.Of<IEaReminderEmailSender>();
        var service = Service(db, sender);
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(new CreateFollowupRequestDto
        { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendEmail = true }));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateAsync(new CreateFollowupRequestDto
        { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientEmail = "not-an-email" }));

        var followup = await service.CreateAsync(new CreateFollowupRequestDto { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderRecipientEmail = "old@example.com" });
        var updated = await service.UpdateAsync(followup.Id, new UpdateFollowupRequestDto
        { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientEmployeeId = "HRMS-2", ReminderRecipientEmail = "new@example.com" });
        Assert.Equal((null, "HRMS-2", "new@example.com", true),
            (updated.ReminderRecipientUserId, updated.Recipient!.EmployeeId, updated.Recipient.Email, updated.ReminderSendEmail));
    }

    [Fact]
    public async Task EmailSenderFailure_PropagatesAndMissingNameUsesCleanGreeting()
    {
        await using var db = MakeDb();
        var sender = new Mock<IEaReminderEmailSender>();
        sender.Setup(x => x.SendAsync(It.IsAny<EaReminderEmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleException("Unable to send reminder email."));
        var service = Service(db, sender.Object);
        var followup = await service.CreateAsync(new CreateFollowupRequestDto
        { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientEmail = "recipient@example.com" });

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SendEmailAsync(followup.Id));
        sender.Verify(x => x.SendAsync(It.Is<EaReminderEmailMessage>(m => m.Body.StartsWith("Hello,") && !m.Body.Contains("Hello ,")), It.IsAny<CancellationToken>()), Times.Once);
    }
}