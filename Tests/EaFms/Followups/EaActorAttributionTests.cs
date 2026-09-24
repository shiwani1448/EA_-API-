using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
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
/// Frontend-supplied actor snapshot (employeeId + employeeName) on TAT rules, Business Modules,
/// Followups and Followup cycles. EA only trims/stores it: no Users, Employees or HRMS lookup,
/// and audit timestamps are always server-controlled.
/// </summary>
public class EaActorAttributionTests
{
    private static readonly ICurrentUserService Placeholder = Mock.Of<ICurrentUserService>(u => u.UserId == 0);

    private static BusinessModuleService Modules(EaFmsDbContext db, Mock<IAuditService>? audit = null) =>
        new(db, new BusinessModuleRepository(db), Placeholder, (audit ?? new Mock<IAuditService>()).Object);

    // Followup actors come from the request token (ICurrentUserService), never from body fields.
    private static readonly ICurrentUserService Siddhi = FollowupTestSupport.User("S5I-1013", "Siddhi Jadhav");

    private static FollowupService Followups(EaFmsDbContext db, Mock<IAuditService>? audit = null, ICurrentUserService? user = null) => new(
        new FollowupRepository(db), db, Mapper, user ?? Siddhi, (audit ?? new Mock<IAuditService>()).Object,
        new FollowupSourceResolver(db), FollowupTestSupport.EaTasks(db), new TatRuleRepository(db));

    private static SaveBusinessModuleDto Module(string name, bool active = true, string? id = "S5I-1013", string? actorName = "Siddhi Jadhav") =>
        new() { Name = name, IsActive = active, EmployeeId = id, EmployeeName = actorName };

    // ---------------- helper ----------------
    [Fact]
    public void ActorSnapshot_TrimsBlankToNull_AndBoundsLength()
    {
        Assert.Equal(new EaActorSnapshot("S5I-1013", "Siddhi Jadhav"), EaActorSnapshot.From("  S5I-1013 ", " Siddhi Jadhav "));
        Assert.Same(EaActorSnapshot.None, EaActorSnapshot.From("  ", null));
        Assert.Equal("Siddhi Jadhav", EaActorSnapshot.From("S5I-1013", "Siddhi Jadhav").DisplayName);
        Assert.Equal("S5I-1013", EaActorSnapshot.From("S5I-1013", null).DisplayName);
        Assert.Throws<BadRequestException>(() => EaActorSnapshot.From(new string('x', 101), "n"));
        Assert.Throws<BadRequestException>(() => EaActorSnapshot.From("i", new string('x', 101)));
    }

    // ---------------- 5, no Users dependency, server-controlled dates ----------------
    [Fact]
    public void Services_HaveNoUsersEmployeesOrHrmsDependency_AndRequestsCarryNoAuditTimestamps()
    {
        foreach (var t in new[] { typeof(TatRuleService), typeof(BusinessModuleService), typeof(FollowupService), typeof(FollowupCycleService) })
            foreach (var p in t.GetConstructors().Single().GetParameters())
            {
                Assert.DoesNotContain("AppDbContext", p.ParameterType.Name);
                Assert.DoesNotContain("ActorResolver", p.ParameterType.Name);
            }
        foreach (var t in new[] { typeof(SaveTatRuleDto), typeof(SaveBusinessModuleDto), typeof(CreateFollowupRequestDto),
                     typeof(UpdateFollowupRequestDto), typeof(RecordFollowupRequestDto), typeof(CreateFollowupCycleRequestDto), typeof(EaActorRequestDto) })
        {
            var names = t.GetProperties().Select(p => p.Name).ToHashSet();
            Assert.Contains("EmployeeId", names);
            Assert.Contains("EmployeeName", names);
            Assert.DoesNotContain("CreatedDate", names);
            Assert.DoesNotContain("ModifiedDate", names);
            Assert.DoesNotContain("FollowedUpAt", names);
        }
    }

    // ---------------- Business modules ----------------
    [Fact]
    public async Task BusinessModule_Create_StoresCreatorSnapshot_WithServerDate()
    {
        await using var db = MakeDb();
        var before = DateTime.UtcNow.AddSeconds(-2);

        var m = await Modules(db).CreateAsync(Module("Vendor Management"), default);

        Assert.Equal(("S5I-1013", "Siddhi Jadhav", "Siddhi Jadhav"), (m.CreatedByEmployeeId, m.CreatedByEmployeeName, m.CreatedBy));
        Assert.InRange(m.CreatedDate, before, DateTime.UtcNow.AddSeconds(2));
        Assert.Null(m.ModifiedByEmployeeId);
        Assert.Null(m.ModifiedDate);
        var row = await db.BusinessModules.AsNoTracking().SingleAsync();
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (row.CreatedByEmployeeId, row.CreatedByEmployeeName));
    }

    [Fact]
    public async Task BusinessModule_Update_StoresModifierSnapshot_KeepsCreator_AndServerDate()
    {
        await using var db = MakeDb();
        var svc = Modules(db);
        var created = await svc.CreateAsync(Module("Vendor Management"), default);

        var updated = await svc.UpdateAsync(created.Id, Module("Vendor Management", true, "S5I-2000", "Aman Verma") , default);

        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (updated.CreatedByEmployeeId, updated.CreatedByEmployeeName));
        Assert.Equal(("S5I-2000", "Aman Verma", "Aman Verma"), (updated.ModifiedByEmployeeId, updated.ModifiedByEmployeeName, updated.ModifiedBy));
        Assert.Equal(created.CreatedDate, updated.CreatedDate);
        Assert.InRange(updated.ModifiedDate!.Value, DateTime.UtcNow.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
    }

    [Fact]
    public async Task BusinessModule_WithoutActor_FallsBackToLegacyAttribution_AndLeavesSnapshotNull()
    {
        await using var db = MakeDb();

        var m = await Modules(db).CreateAsync(Module("Legacy", true, null, null), default);

        Assert.Equal("system", m.CreatedBy);
        Assert.Null(m.CreatedByEmployeeId);
        Assert.Null(m.CreatedByEmployeeName);
    }

    // ---------------- DELETE = safe deactivate ----------------
    [Fact]
    public async Task BusinessModule_Delete_DeactivatesWithoutDeletingAnyReferencedData()
    {
        await using var db = MakeDb();
        var svc = Modules(db);
        var m = await svc.CreateAsync(Module("Vendor Management"), default);
        db.Tasks.Add(new EaTask { BusinessModuleId = m.Id, ModuleName = m.Name, BusinessRecordId = "V-1", Task = "t", ExecutionStatus = "NotStarted", CreatedBy = "1", CreatedDate = Base });
        db.TatRules.Add(new TatRule { BusinessModuleId = m.Id, ModuleName = m.Name, Type = "a", Subtype = "b", TatMinutes = 5, IsActive = true, CreatedBy = "seed", CreatedDate = Base });
        db.Followups.Add(new Followup { BusinessModuleId = m.Id, BusinessRecordId = "V-1", DueAt = Base, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();
        var audit = new Mock<IAuditService>();

        var result = await Modules(db, audit).DeactivateAsync(m.Id, new EaActorRequestDto { EmployeeId = " S5I-1013 ", EmployeeName = "Siddhi Jadhav" }, default);

        Assert.False(result.IsActive);
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (result.ModifiedByEmployeeId, result.ModifiedByEmployeeName));
        Assert.NotNull(result.ModifiedDate);
        var row = await db.BusinessModules.AsNoTracking().SingleAsync();
        Assert.False(row.IsDeleted);                                   // never physically or logically deleted
        Assert.Equal(1, await db.Tasks.CountAsync());
        Assert.Equal(1, await db.TatRules.CountAsync(r => !r.IsDeleted));
        Assert.Equal(1, await db.Followups.CountAsync());
        audit.Verify(a => a.AddAudit("BUSINESS_MODULE_DEACTIVATE", "BusinessModule", nameof(BusinessModule), m.Id.ToString(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task BusinessModule_Delete_MissingModuleIs404_ProtectedModulesAreRefused_RepeatIsIdempotent()
    {
        await using var db = MakeDb();
        var svc = Modules(db);
        var protectedModule = await svc.CreateAsync(Module("Meeting"), default);
        var normal = await svc.CreateAsync(Module("Vendor Management"), default);

        await Assert.ThrowsAsync<NotFoundException>(() => svc.DeactivateAsync(99999, null, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.DeactivateAsync(protectedModule.Id, null, default));
        Assert.True((await svc.GetByIdAsync(protectedModule.Id, default)).IsActive);

        var first = await svc.DeactivateAsync(normal.Id, null, default);
        var second = await svc.DeactivateAsync(normal.Id, new EaActorRequestDto { EmployeeId = "X", EmployeeName = "Y" }, default);
        Assert.False(second.IsActive);
        Assert.Equal(first.ModifiedDate, second.ModifiedDate);         // repeat changes nothing
        Assert.Null(second.ModifiedByEmployeeId);
    }

    [Fact]
    public async Task BusinessModule_Put_StillReactivates_AndRecordsTheModifier()
    {
        await using var db = MakeDb();
        var svc = Modules(db);
        var m = await svc.CreateAsync(Module("Vendor Management"), default);
        await svc.DeactivateAsync(m.Id, null, default);

        var back = await svc.UpdateAsync(m.Id, Module("Vendor Management", true, "S5I-1013", "Siddhi Jadhav"), default);

        Assert.True(back.IsActive);
        Assert.Equal("Siddhi Jadhav", back.ModifiedByEmployeeName);
    }

    [Fact]
    public void BusinessModulesController_NowExposesHttpDelete()
    {
        var delete = typeof(Jarvis5.Controllers.BusinessModulesController).GetMethod("Delete");

        Assert.NotNull(delete);
        Assert.Contains(delete!.GetCustomAttributes(), a => a is Microsoft.AspNetCore.Mvc.HttpDeleteAttribute d && d.Template == "{id:long}");
    }

    // ---------------- Followup ----------------
    private static async Task<(Seed S, FollowupService Svc)> FollowupSeedAsync(Mock<IAuditService>? audit = null, ICurrentUserService? user = null)
    {
        var s = await SeedAsync();
        return (s, Followups(s.Db, audit, user));
    }

    [Theory]
    [InlineData("Meeting")]
    [InlineData("Travel & Hospitality")]
    [InlineData("EA Approval")]
    [InlineData("Delegation")]
    public async Task Followup_Create_StoresActorSeparatelyFromRecipientAndSource_ForEveryModule(string module)
    {
        var (s, svc) = await FollowupSeedAsync(); await using var _ = s.Db;
        var m = s.Modules[module];
        var record = module switch
        {
            "Meeting" => s.Meeting.Id.ToString(), "Travel & Hospitality" => s.Travel.Id.ToString(),
            "Delegation" => s.Delegation.Id.ToString(), _ => s.Approval.ReferenceNo
        };
        s.Db.Tasks.Add(new EaTask { BusinessModuleId = m.Id, ModuleName = m.Name, BusinessRecordId = record, Task = "Prepare MOM", ExecutionStatus = "InProgress", CreatedBy = "1", CreatedDate = Base });
        await s.Db.SaveChangesAsync();

        var f = await svc.CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = m.Id, BusinessRecordId = record, Remark = "chase", DueAt = Base.AddDays(2),
            ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientEmployeeId = "S5I-2000",
            ReminderRecipientName = "Aman Verma", ReminderRecipientEmail = "aman@example.com",
            EmployeeId = "FORGED", EmployeeName = "Someone Else"   // body identity is ignored
        });

        // actor (from the token)
        Assert.Equal(("S5I-1013", "Siddhi Jadhav", "Siddhi Jadhav"), (f.CreatedByEmployeeId, f.CreatedByEmployeeName, f.CreatedBy));
        Assert.InRange(f.CreatedDate, DateTime.UtcNow.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
        // recipient (separate)
        Assert.Equal(("S5I-2000", "Aman Verma", "aman@example.com"), (f.ReminderRecipientEmployeeId, f.ReminderRecipientName, f.ReminderRecipientEmail));
        // source + derived task context
        Assert.Equal((m.Id, record, module, "Prepare MOM", "InProgress"), (f.BusinessModuleId, f.BusinessRecordId, f.ModuleName, f.Task, f.Stage));
        Assert.NotNull(f.SourceEaTaskId);
        Assert.NotNull(f.EaTaskId);                       // the Followup's own task
        Assert.NotEqual(f.SourceEaTaskId, f.EaTaskId);
        var row = await s.Db.Followups.AsNoTracking().SingleAsync();
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (row.CreatedByEmployeeId, row.CreatedByEmployeeName));
        Assert.Equal("Aman Verma", row.ReminderRecipientName);
    }

    [Fact]
    public async Task Followup_Update_StoresModifier_KeepsCreatorAndCreatedDate_WithServerModifiedDate()
    {
        var (s, svc) = await FollowupSeedAsync(); await using var _ = s.Db;
        var created = await svc.CreateAsync(new CreateFollowupRequestDto { Subject = "s" });

        var updated = await Followups(s.Db, user: FollowupTestSupport.User("S5I-2000", "Aman Verma"))
            .UpdateAsync(created.Id, new UpdateFollowupRequestDto { Subject = "s2", EmployeeId = "FORGED", EmployeeName = "Someone Else" });

        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (updated.CreatedByEmployeeId, updated.CreatedByEmployeeName));
        Assert.Equal(created.CreatedDate, updated.CreatedDate);
        Assert.Equal(("S5I-2000", "Aman Verma", "Aman Verma"), (updated.ModifiedByEmployeeId, updated.ModifiedByEmployeeName, updated.ModifiedBy));
        Assert.InRange(updated.ModifiedDate!.Value, DateTime.UtcNow.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
    }

    [Fact]
    public async Task Followup_WithoutTokenIdentity_NeverStoresZero_AndLeavesSnapshotNull()
    {
        var (s, svc) = await FollowupSeedAsync(user: Placeholder); await using var _ = s.Db;

        var f = await svc.CreateAsync(new CreateFollowupRequestDto { Subject = "s", EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav" });

        Assert.Equal("system", f.CreatedBy);             // required column; "0" is never persisted
        Assert.Null(f.CreatedByEmployeeId);              // body identity is not trusted
        Assert.Null(f.CreatedByEmployeeName);
    }

    [Fact]
    public async Task Followup_Actor_IsIncludedInAuditPayloads()
    {
        var audit = new Mock<IAuditService>();
        var (s, svc) = await FollowupSeedAsync(audit); await using var _ = s.Db;

        var f = await svc.CreateAsync(new CreateFollowupRequestDto { Subject = "s" });
        await svc.UpdateAsync(f.Id, new UpdateFollowupRequestDto());

        foreach (var action in new[] { "FOLLOWUP_CREATE", "FOLLOWUP_UPDATE" })
            audit.Verify(a => a.AddAudit(action, "Followup", nameof(Followup), f.Id.ToString(), It.IsAny<object?>(),
                It.Is<object?>(o => o != null && o.ToString()!.Contains("S5I-1013") && o.ToString()!.Contains("Siddhi Jadhav")), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Followup_BodyActorFields_AreIgnored_EvenWhenOversized()
    {
        var (s, svc) = await FollowupSeedAsync(); await using var _ = s.Db;

        var f = await svc.CreateAsync(new CreateFollowupRequestDto { EmployeeName = new string('n', 101) });

        Assert.Equal("Siddhi Jadhav", f.CreatedByEmployeeName);
    }

    // ---------------- record-followup ----------------
    [Fact]
    public async Task RecordFollowup_StoresPerformingActor_WithServerTimestamp_AndAppendsOneCycle()
    {
        var (s, svc) = await FollowupSeedAsync(); await using var _ = s.Db;
        var f = await svc.CreateAsync(new CreateFollowupRequestDto { Subject = "s" });

        await Followups(s.Db, user: FollowupTestSupport.User("S5I-3000", "Riya Shah"))
            .RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { Note = "spoke to Aman", EmployeeId = "FORGED", EmployeeName = "Someone Else" });
        var after = await svc.GetByIdAsync(f.Id);

        Assert.Equal(("S5I-3000", "Riya Shah"), (after.ModifiedByEmployeeId, after.ModifiedByEmployeeName));
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (after.CreatedByEmployeeId, after.CreatedByEmployeeName));   // creator untouched
        Assert.InRange(after.LastFollowupAt!.Value, DateTime.UtcNow.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
        Assert.Equal(1, await s.Db.FollowupCycles.CountAsync());   // one recorded follow-up = one history row (see FinalConsistencyTests)
    }

    // ---------------- cycles ----------------
    [Fact]
    public async Task FollowupCycle_Create_StoresFollowedUpByActor_WithServerFollowedUpAt()
    {
        await using var db = MakeDb();
        var parent = new Followup { Id = 5, DueAt = Base, CreatedBy = "seed", CreatedDate = Base };
        FollowupCycle? saved = null;
        var repo = new Mock<IFollowupCycleRepository>();
        repo.Setup(r => r.LockParentAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(parent);
        repo.Setup(r => r.GetMaximumSequenceAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        repo.Setup(r => r.AddAsync(It.IsAny<FollowupCycle>(), It.IsAny<CancellationToken>()))
            .Callback<FollowupCycle, CancellationToken>((c, _) => { saved = c; db.FollowupCycles.Add(c); }).Returns(Task.CompletedTask);
        var svc = new FollowupCycleService(db, repo.Object, Mock.Of<IAuditService>(), Placeholder, new CreateFollowupCycleRequestDtoValidator());

        var r = await svc.CreateAsync(5, new CreateFollowupCycleRequestDto { Note = "called", EmployeeId = " S5I-1013 ", EmployeeName = "Siddhi Jadhav" }, default);

        Assert.Equal(("S5I-1013", "Siddhi Jadhav", "Siddhi Jadhav"), (r.FollowedUpByEmployeeId, r.FollowedUpByEmployeeName, r.CreatedBy));
        Assert.InRange(r.FollowedUpAt, DateTime.UtcNow.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (saved!.FollowedUpByEmployeeId, saved.FollowedUpByEmployeeName));
        Assert.Equal((null, null), (parent.ReminderRecipientEmployeeId, parent.ReminderRecipientName));   // actor is not written into recipient fields
    }

    [Fact]
    public async Task FollowupCycle_WithoutActor_KeepsLegacyAttribution()
    {
        await using var db = MakeDb();
        var parent = new Followup { Id = 5, DueAt = Base, CreatedBy = "seed", CreatedDate = Base };
        var repo = new Mock<IFollowupCycleRepository>();
        repo.Setup(r => r.LockParentAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(parent);
        repo.Setup(r => r.AddAsync(It.IsAny<FollowupCycle>(), It.IsAny<CancellationToken>()))
            .Callback<FollowupCycle, CancellationToken>((c, _) => db.FollowupCycles.Add(c)).Returns(Task.CompletedTask);
        var svc = new FollowupCycleService(db, repo.Object, Mock.Of<IAuditService>(), Placeholder, new CreateFollowupCycleRequestDtoValidator());

        var r = await svc.CreateAsync(5, new CreateFollowupCycleRequestDto { Note = "x" }, default);

        Assert.Equal("system", r.CreatedBy);   // "0" is never persisted as an actor
        Assert.Null(r.FollowedUpByEmployeeId);
    }

    // ---------------- regression ----------------
    [Fact]
    public async Task SendWhatsApp_SendEmail_AndMultipleFollowups_StillWork_WithActorAttributedFollowups()
    {
        var (s, _) = await FollowupSeedAsync();
        await using var __ = s.Db;
        var svc = Followups(s.Db);
        var m = s.Modules["Meeting"].Id;
        var dto = new CreateFollowupRequestDto
        {
            BusinessModuleId = m, BusinessRecordId = s.Meeting.Id.ToString(), DueAt = Base.AddDays(3), ReminderAt = Base.AddDays(1),
            ReminderSendEmail = true, ReminderRecipientEmail = "aman@example.com", ReminderSendWhatsApp = true, ReminderWhatsAppNumber = "9999999999",
            EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav"
        };

        var a = await svc.CreateAsync(dto);
        var b = await svc.CreateAsync(dto);
        var handoff = await svc.SendWhatsAppAsync(a.Id);
        var email = await svc.SendEmailAsync(a.Id);

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal("9999999999", handoff.Phone);
        Assert.Equal("aman@example.com", email.Email);
        Assert.StartsWith("mailto:aman@example.com?subject=", email.MailtoUrl);
    }
}
