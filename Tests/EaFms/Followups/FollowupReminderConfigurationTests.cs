using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static Jarvis5.Tests.EaFms.Followups.FollowupBusinessApiTests;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// Reminder configuration stores HRMS recipient snapshots. ReminderRecipientUserId remains only
/// for legacy compatibility; no Users lookup is part of this flow.
/// </summary>
public class FollowupReminderConfigurationTests
{
    private static readonly DateTime Remind = Base.AddDays(1);

    private static FollowupService Svc(EaFmsDbContext db, Mock<IAuditService>? audit = null) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(u => u.UserId == 7 && u.UserName == "EA User"),
        (audit ?? new Mock<IAuditService>()).Object, new FollowupSourceResolver(db));

    private static CreateFollowupRequestDto Create(Action<CreateFollowupRequestDto>? tweak = null)
    {
        var d = new CreateFollowupRequestDto { Subject = "s", DueAt = Base.AddDays(3) };
        tweak?.Invoke(d);
        return d;
    }

    private static UpdateFollowupRequestDto Update(Action<UpdateFollowupRequestDto>? tweak = null)
    {
        var d = new UpdateFollowupRequestDto { Subject = "s", DueAt = Base.AddDays(3) };
        tweak?.Invoke(d);
        return d;
    }

    [Fact]
    public async Task NoReminderChannels_IsValid_AndDefaultsToFalseAndNull()
    {
        await using var db = MakeDb();

        var f = await Svc(db).CreateAsync(Create());

        Assert.False(f.ReminderSendEmail);
        Assert.False(f.ReminderSendWhatsApp);
        Assert.Null(f.ReminderRecipientUserId);
        Assert.Null(f.ReminderWhatsAppNumber);
        Assert.Null(f.ReminderAt);
    }

    [Fact]
    public async Task ReminderAtWithBothChannelsFalse_IsValid()
    {
        await using var db = MakeDb();

        var f = await Svc(db).CreateAsync(Create(d => d.ReminderAt = Remind));

        Assert.Equal(Remind, f.ReminderAt);
        Assert.False(f.ReminderSendEmail || f.ReminderSendWhatsApp);
    }

    [Fact]
    public async Task EmailReminder_StoresTimeFlagAndRecipientSnapshot()
    {
        await using var db = MakeDb();

        var f = await Svc(db).CreateAsync(Create(d => { d.ReminderAt = Remind; d.ReminderSendEmail = true; d.ReminderRecipientEmployeeId = "HRMS-1"; d.ReminderRecipientEmail = "anurag@example.com"; }));

        Assert.Equal((Remind, true, false, (int?)null, (string?)null), (f.ReminderAt, f.ReminderSendEmail, f.ReminderSendWhatsApp, f.ReminderRecipientUserId, f.ReminderWhatsAppNumber));
        var row = await db.Followups.AsNoTracking().SingleAsync();
        Assert.Equal(("HRMS-1", "anurag@example.com"), (row.ReminderRecipientEmployeeId, row.ReminderRecipientEmail));
    }

    [Fact]
    public async Task EmailReminder_RejectsInvalidSnapshot_MissingTime_AndMissingRecipient()
    {
        await using var db = MakeDb();
        var svc = Svc(db);

        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(d => { d.ReminderAt = Remind; d.ReminderSendEmail = true; d.ReminderRecipientEmail = "not-an-email"; })));
        var missing = await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(d => { d.ReminderAt = Remind; d.ReminderSendEmail = true; })));
        Assert.Contains("ReminderRecipientEmail", missing.Message);
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(d => { d.ReminderSendEmail = true; d.ReminderRecipientEmail = "anurag@example.com"; })));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(d => { d.ReminderAt = Remind; d.ReminderSendEmail = true; })));
        Assert.Equal(0, await db.Followups.CountAsync());
    }

    [Fact]
    public async Task RecipientUserId_IsLegacyOnly_AndMustOnlyBePositiveWhenSupplied()
    {
        await using var db = MakeDb();
        var svc = Svc(db);        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(d => d.ReminderRecipientUserId = 0)));
        Assert.True((await svc.CreateAsync(Create(d => d.ReminderRecipientUserId = 99))).Id > 0);
    }

    [Fact]
    public async Task WhatsAppReminder_StoresTrimmedNumber_WithoutRecipient()
    {
        await using var db = MakeDb();

        var f = await Svc(db).CreateAsync(Create(d => { d.ReminderAt = Remind; d.ReminderSendWhatsApp = true; d.ReminderWhatsAppNumber = " +91 98765 43210 "; }));

        Assert.Equal((true, false, null, "+91 98765 43210"), (f.ReminderSendWhatsApp, f.ReminderSendEmail, f.ReminderRecipientUserId, f.ReminderWhatsAppNumber));
    }

    [Fact]
    public async Task WhatsAppReminder_RequiresTimeAndNumber_ButNumberIsOptionalWhenOff()
    {
        await using var db = MakeDb();
        var svc = Svc(db);

        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(d => { d.ReminderSendWhatsApp = true; d.ReminderWhatsAppNumber = "123"; })));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(d => { d.ReminderAt = Remind; d.ReminderSendWhatsApp = true; })));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(d => { d.ReminderAt = Remind; d.ReminderSendWhatsApp = true; d.ReminderWhatsAppNumber = "  "; })));
        Assert.True((await svc.CreateAsync(Create(d => d.ReminderAt = Remind))).Id > 0);
    }

    [Fact]
    public async Task EmailAndWhatsApp_CanBothBeSelected()
    {
        await using var db = MakeDb();

        var f = await Svc(db).CreateAsync(Create(d =>
        { d.ReminderAt = Remind; d.ReminderSendEmail = true; d.ReminderRecipientEmail = "anurag@example.com"; d.ReminderSendWhatsApp = true; d.ReminderRecipientUserId = 1; d.ReminderWhatsAppNumber = "9999999999"; }));

        Assert.True(f.ReminderSendEmail && f.ReminderSendWhatsApp);
        Assert.Equal((1, "9999999999"), (f.ReminderRecipientUserId, f.ReminderWhatsAppNumber));
    }

    [Fact]
    public async Task Update_ChangesThenClearsTheReminderConfiguration_WithoutTouchingTheFollowup()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var svc = Svc(s.Db);
        var created = await svc.CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = s.Modules["Meeting"].Id, BusinessRecordId = s.Meeting.Id.ToString(), Subject = "s", DueAt = Base.AddDays(3),
            NextFollowupAt = Base.AddDays(5), ReminderAt = Remind
        });

        var updated = await svc.UpdateAsync(created.Id, Update(d =>
        { d.NextFollowupAt = Base.AddDays(5); d.ReminderAt = Remind.AddHours(2); d.ReminderSendEmail = true; d.ReminderRecipientEmail = "anurag@example.com"; d.ReminderSendWhatsApp = true; d.ReminderRecipientUserId = 1; d.ReminderWhatsAppNumber = "555"; }));
        Assert.Equal((Remind.AddHours(2), true, true, 1, "555"), (updated.ReminderAt, updated.ReminderSendEmail, updated.ReminderSendWhatsApp, updated.ReminderRecipientUserId, updated.ReminderWhatsAppNumber));

        var cleared = await svc.UpdateAsync(created.Id, Update(d => d.NextFollowupAt = Base.AddDays(5)));
        Assert.Equal((null, false, false, null, null), (cleared.ReminderAt, cleared.ReminderSendEmail, cleared.ReminderSendWhatsApp, cleared.ReminderRecipientUserId, cleared.ReminderWhatsAppNumber));
        Assert.Equal(1, await s.Db.Followups.CountAsync());
        Assert.Equal((s.Modules["Meeting"].Id, s.Meeting.Id.ToString()), (cleared.BusinessModuleId, cleared.BusinessRecordId));
        Assert.Equal(Base.AddDays(5), cleared.NextFollowupAt);   // NextFollowupAt is independent of the reminder
        Assert.Null(cleared.CompletedAt);
    }

    [Fact]
    public async Task Update_AppliesTheSameReminderRules()
    {
        await using var db = MakeDb();
        var svc = Svc(db);
        var f = await svc.CreateAsync(Create());

        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateAsync(f.Id, Update(d => { d.ReminderAt = Remind; d.ReminderSendEmail = true; d.ReminderRecipientEmail = "not-an-email"; })));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.UpdateAsync(f.Id, Update(d => { d.ReminderSendWhatsApp = true; d.ReminderWhatsAppNumber = "1"; })));
        Assert.False((await svc.GetByIdAsync(f.Id)).ReminderSendWhatsApp);   // failed update changed nothing
    }

    [Fact]
    public async Task GetSingleAndList_ReturnTheReminderConfiguration()
    {
        await using var db = MakeDb();
        var svc = Svc(db);
        var f = await svc.CreateAsync(Create(d => { d.ReminderAt = Remind; d.ReminderSendEmail = true; d.ReminderRecipientEmployeeId = "HRMS-1"; d.ReminderRecipientEmail = "anurag@example.com"; }));

        var single = await svc.GetByIdAsync(f.Id);
        var listed = Assert.Single((await svc.GetPagedAsync(new FollowupListQueryDto())).Items);

        foreach (var r in new[] { single, listed })
            Assert.Equal((Remind, true, false, (int?)null, (string?)null), (r.ReminderAt, r.ReminderSendEmail, r.ReminderSendWhatsApp, r.ReminderRecipientUserId, r.ReminderWhatsAppNumber));
    }

    [Fact]
    public void ReminderAtAfterDueAt_IsStillRejectedByTheExistingRule()
    {
        Assert.False(new CreateFollowupRequestDtoValidator().Validate(Create(d => d.ReminderAt = Base.AddDays(4))).IsValid);   // due = +3 days
        Assert.False(new UpdateFollowupRequestDtoValidator().Validate(Update(d => d.ReminderAt = Base.AddDays(4))).IsValid);
        Assert.True(new CreateFollowupRequestDtoValidator().Validate(Create(d => d.ReminderAt = Base.AddDays(3))).IsValid);
    }

    [Fact]
    public void Validators_HaveNoMandatoryReminderFields_AndLimitTheNumberLength()
    {
        Assert.True(new CreateFollowupRequestDtoValidator().Validate(new CreateFollowupRequestDto()).IsValid);
        Assert.True(new UpdateFollowupRequestDtoValidator().Validate(new UpdateFollowupRequestDto()).IsValid);
        Assert.False(new CreateFollowupRequestDtoValidator().Validate(Create(d => d.ReminderWhatsAppNumber = new string('9', 51))).IsValid);
        Assert.False(new UpdateFollowupRequestDtoValidator().Validate(Update(d => d.ReminderRecipientUserId = 0)).IsValid);
    }

    [Fact]
    public void RequestJson_ExposesTheApprovedReminderContract_AndNoEmailAddressField()
    {
        var dto = JsonSerializer.Deserialize<CreateFollowupRequestDto>(
            """{"reminderAt":"2026-10-02T09:00:00Z","reminderSendEmail":true,"reminderSendWhatsApp":true,"reminderRecipientUserId":3,"reminderWhatsAppNumber":"98"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal((true, true, 3, "98"), (dto.ReminderSendEmail, dto.ReminderSendWhatsApp, dto.ReminderRecipientUserId, dto.ReminderWhatsAppNumber));
        foreach (var t in new[] { typeof(FollowupResponseDto), typeof(CreateFollowupRequestDto), typeof(UpdateFollowupRequestDto) })
            Assert.Contains(t.GetProperties(), p => p.Name == "ReminderRecipientEmail");
    }

    [Theory]
    [InlineData("Meeting")]
    [InlineData("Travel & Hospitality")]
    [InlineData("EA Approval")]
    [InlineData("Delegation")]
    public async Task EveryModuleFollowup_SupportsReminderConfiguration_WithoutSideEffects(string module)
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var recordId = module switch
        {
            "Meeting" => s.Meeting.Id.ToString(), "Travel & Hospitality" => s.Travel.Id.ToString(),
            "Delegation" => s.Delegation.Id.ToString(), _ => s.Approval.ReferenceNo
        };
        var tasks = await s.Db.Tasks.CountAsync();
        var audit = new Mock<IAuditService>();
        var svc = Svc(s.Db, audit);

        var f = await svc.CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = s.Modules[module].Id, BusinessRecordId = recordId, DueAt = Base.AddDays(3),
            ReminderAt = Remind, ReminderSendEmail = true, ReminderRecipientEmail = "anurag@example.com", ReminderSendWhatsApp = true, ReminderRecipientUserId = 1, ReminderWhatsAppNumber = "9"
        });
        await svc.UpdateAsync(f.Id, Update(d => { d.ReminderAt = Remind; d.ReminderSendWhatsApp = true; d.ReminderWhatsAppNumber = "8"; }));

        Assert.True(f.ReminderSendEmail);
        Assert.Equal(1, await s.Db.Followups.CountAsync());
        Assert.Equal(tasks, await s.Db.Tasks.CountAsync());
        Assert.Equal(0, await s.Db.Escalations.CountAsync());
        Assert.Equal(0, await s.Db.Notifications.CountAsync());
        audit.Verify(a => a.AddAudit("FOLLOWUP_CREATE", "Followup", nameof(Followup), f.Id.ToString(), It.IsAny<object?>(),
            It.Is<object?>(o => o != null && o.ToString()!.Contains("ReminderSendWhatsApp = True") && o.ToString()!.Contains("ReminderRecipientUserId = 1")), It.IsAny<string?>()), Times.Once);
        audit.Verify(a => a.AddAudit("FOLLOWUP_UPDATE", "Followup", nameof(Followup), f.Id.ToString(), It.IsAny<object?>(),
            It.Is<object?>(o => o != null && o.ToString()!.Contains("ReminderWhatsAppNumber = 8")), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task FutureModuleFollowup_SupportsReminderConfiguration()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var future = s.Modules["Vendor Management"];
        s.Db.Tasks.Add(new EaTask { BusinessModuleId = future.Id, ModuleName = future.Name, BusinessRecordId = "V-1", Task = "t", ExecutionStatus = "NotStarted", CreatedBy = "1", CreatedDate = Base });
        await s.Db.SaveChangesAsync();

        var f = await Svc(s.Db).CreateAsync(new CreateFollowupRequestDto
        { BusinessModuleId = future.Id, BusinessRecordId = "V-1", DueAt = Base.AddDays(3), ReminderAt = Remind, ReminderSendWhatsApp = true, ReminderWhatsAppNumber = "7" });

        Assert.True(f.ReminderSendWhatsApp);
    }
}
