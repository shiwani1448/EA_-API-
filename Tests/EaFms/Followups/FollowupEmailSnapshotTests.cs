using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Controllers;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// POST /api/ea/followups/{id}/send-email is a user-initiated Email handoff (same pattern as send-whatsapp): the backend returns the
/// recipient snapshot, subject, body and a ready-to-open mailto: URL. It sends nothing, so no SMTP configuration or connection is involved.
/// </summary>
public class FollowupEmailSnapshotTests
{
    // Note: FollowupService has no email-sender / IConfiguration dependency at all.
    private static FollowupService Service(EaFmsDbContext db) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(x => x.UserId == 7 && x.UserName == "EA User"),
        Mock.Of<IAuditService>(), new FollowupSourceResolver(db));

    [Fact]
    public async Task HrmsSnapshot_EmailAction_ReturnsMailtoFromTheStoredSnapshot_NeverUsesUsers()
    {
        var seed = await SeedAsync(); await using var _ = seed.Db;
        var module = seed.Modules["Meeting"];
        seed.Db.Tasks.Add(new EaTask { BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), ModuleName = module.Name, Task = "Prepare MOM", ExecutionStatus = "Completed", CreatedBy = "seed", CreatedDate = Base });
        await seed.Db.SaveChangesAsync();
        var service = Service(seed.Db);

        var created = await service.CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = module.Id, BusinessRecordId = seed.Meeting.Id.ToString(), DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1),
            ReminderSendEmail = true, ReminderRecipientEmployeeId = "HRMS-101", ReminderRecipientName = "HRMS Recipient",
            ReminderRecipientEmail = "recipient@example.com", Remark = "Please complete the MOM."
        });
        Assert.Null(created.ReminderRecipientUserId);
        Assert.Equal(("HRMS-101", "HRMS Recipient", "recipient@example.com", true, false),
            (created.Recipient!.EmployeeId, created.Recipient.Name, created.Recipient.Email, created.ReminderSendEmail, created.ReminderSendWhatsApp));

        var action = await service.SendEmailAsync(created.Id);

        Assert.Equal("recipient@example.com", action.Email);
        Assert.Equal("Reminder / Follow-up - Prepare MOM", action.Subject);
        Assert.Contains("Hello HRMS Recipient,", action.Body);
        Assert.Contains("Module: Meeting", action.Body);
        Assert.Contains($"Task ID: {created.EaTaskId}", action.Body);
        Assert.Contains("Task: Prepare MOM", action.Body);
        Assert.Contains($"Follow-up Date: {created.ReminderAt:O}", action.Body);
        Assert.Contains("Remark: Please complete the MOM.", action.Body);
        Assert.StartsWith("mailto:recipient@example.com?subject=", action.MailtoUrl);
    }

    [Fact]
    public async Task EmailSnapshot_IsRequiredAndValidated_AndUpdateNeedsNoUserId()
    {
        await using var db = MakeDb();
        var service = Service(db);
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
    public async Task MissingRecipientName_UsesACleanGreeting_AndNothingIsSent()
    {
        await using var db = MakeDb();
        var service = Service(db);
        var followup = await service.CreateAsync(new CreateFollowupRequestDto
        { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientEmail = "recipient@example.com" });

        var action = await service.SendEmailAsync(followup.Id);

        Assert.StartsWith("Hello,", action.Body);
        Assert.DoesNotContain("Hello ,", action.Body);
    }

    // ---------------- mailto URI ----------------
    [Fact]
    public async Task Mailto_IsUriEncoded_ForSpacesSpecialCharactersAndNewlines_AndRoundTrips()
    {
        await using var db = MakeDb();
        var service = Service(db);
        var followup = await service.CreateAsync(new CreateFollowupRequestDto
        {
            DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientEmail = "  first.last+tag@example.com ",
            ReminderRecipientName = "Ravi & \"Co\"", Remark = "Line one & two = 100%?\nNext line #3 ñ 日本"
        });

        var action = await service.SendEmailAsync(followup.Id);

        Assert.Equal("first.last+tag@example.com", action.Email);   // trimmed snapshot
        var url = action.MailtoUrl;
        Assert.StartsWith("mailto:first.last%2Btag@example.com?subject=", url);
        // exactly one '?' and two '&'-free parameter values: raw separators cannot appear inside the encoded values
        Assert.Single(url, '?');
        var query = url[(url.IndexOf('?') + 1)..];
        var parts = query.Split('&');
        Assert.Equal(2, parts.Length);
        Assert.StartsWith("subject=", parts[0]);
        Assert.StartsWith("body=", parts[1]);
        foreach (var raw in new[] { ' ', '\n', '\r', '#', '"', '=' })
            Assert.DoesNotContain(raw, parts[0]["subject=".Length..] + parts[1]["body=".Length..]);
        Assert.Contains("%20", parts[0]);
        Assert.Contains("%0D%0A", parts[1]);                       // RFC 6068 line breaks
        Assert.Contains("%26", parts[1]);                          // '&' inside the text
        Assert.DoesNotContain("+", parts[1]);                      // spaces are %20, never '+'
        // decoding gives back the exact text (line breaks as CRLF)
        Assert.Equal(action.Subject, Uri.UnescapeDataString(parts[0]["subject=".Length..]));
        Assert.Equal(action.Body.Replace("\n", "\r\n"), Uri.UnescapeDataString(parts[1]["body=".Length..]));
        Assert.Contains("Hello Ravi & \"Co\",", Uri.UnescapeDataString(parts[1]));
        Assert.Contains("Next line #3 ñ 日本", Uri.UnescapeDataString(parts[1]));
    }

    [Fact]
    public void BuildMailtoUrl_Format_IsRfc6068()
    {
        var url = FollowupService.BuildMailtoUrl("a@b.com", "Hi there", "x\ny");

        Assert.Equal("mailto:a@b.com?subject=Hi%20there&body=x%0D%0Ay", url);
    }

    // ---------------- rejections ----------------
    [Fact]
    public async Task MissingOrInvalidRecipient_IsRejectedWithTheExistingBadRequest_AndNoResolutionFromAnotherSystem()
    {
        await using var db = MakeDb();
        var service = Service(db);
        var noEmail = await service.CreateAsync(new CreateFollowupRequestDto { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1) });
        var emailOff = await service.CreateAsync(new CreateFollowupRequestDto { DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderRecipientEmail = "off@example.com" });

        // reminderSendEmail=false is rejected first
        await Assert.ThrowsAsync<BadRequestException>(() => service.SendEmailAsync(noEmail.Id));
        var off = await Assert.ThrowsAsync<BadRequestException>(() => service.SendEmailAsync(emailOff.Id));
        Assert.Contains("ReminderSendEmail must be true", off.Message);

        // a blank recipient is stored directly on a row that says email is on: still rejected, still no lookup elsewhere
        var row = await db.Followups.FindAsync(noEmail.Id);
        row!.ReminderSendEmail = true; row.ReminderRecipientEmail = "   ";
        await db.SaveChangesAsync();
        var missing = await Assert.ThrowsAsync<BadRequestException>(() => service.SendEmailAsync(noEmail.Id));
        Assert.Contains("ReminderRecipientEmail is required", missing.Message);

        await Assert.ThrowsAsync<NotFoundException>(() => service.SendEmailAsync(987654));
    }

    // ---------------- no SMTP ----------------
    [Fact]
    public void EmailAction_NeedsNoSmtpAndNoConfiguration_TheServiceHasNoSenderOrConfigurationDependency()
    {
        var ctorParameters = typeof(FollowupService).GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType).ToList();

        Assert.DoesNotContain(ctorParameters, t => t == typeof(IEaReminderEmailSender));
        Assert.DoesNotContain(ctorParameters, t => t.Name.Contains("Configuration") || t.Name.Contains("Smtp") || t.Name.Contains("Email"));
        var fields = typeof(FollowupService).GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.FieldType);
        Assert.DoesNotContain(fields, t => t == typeof(IEaReminderEmailSender));
    }

    [Fact]
    public void Route_IsUnchanged_AndReturnsTheHandoffDto_LikeWhatsApp()
    {
        var email = typeof(FollowupsController).GetMethod(nameof(FollowupsController.SendEmail))!;
        var whatsApp = typeof(FollowupsController).GetMethod(nameof(FollowupsController.SendWhatsApp))!;

        Assert.Equal("{id:long}/send-email", email.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Equal("{id:long}/send-whatsapp", whatsApp.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.Contains(email.GetCustomAttributes<ProducesResponseTypeAttribute>(), a => a.Type == typeof(FollowupEmailActionResponseDto) && a.StatusCode == 200);
        Assert.Contains(whatsApp.GetCustomAttributes<ProducesResponseTypeAttribute>(), a => a.Type == typeof(FollowupWhatsAppActionResponseDto) && a.StatusCode == 200);
        Assert.Equal(new[] { "Email", "Subject", "Body", "MailtoUrl" }, typeof(FollowupEmailActionResponseDto).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task WhatsApp_Behavior_IsUnchanged()
    {
        await using var db = MakeDb();
        var service = Service(db);
        var followup = await service.CreateAsync(new CreateFollowupRequestDto
        {
            DueAt = Base.AddDays(2), ReminderAt = Base.AddDays(1), ReminderSendWhatsApp = true, ReminderWhatsAppNumber = "+91 98765 43210", ReminderRecipientName = "Asha"
        });

        var handoff = await service.SendWhatsAppAsync(followup.Id);

        Assert.Equal("+91 98765 43210", handoff.Phone);
        Assert.StartsWith("Hello Asha,", handoff.Message);
        Assert.Equal(new[] { "Phone", "Message" }, typeof(FollowupWhatsAppActionResponseDto).GetProperties().Select(p => p.Name));
    }
}
