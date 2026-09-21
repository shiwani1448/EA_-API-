using System;
using System.Linq;
using System.Reflection;
using System.Threading;
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

/// <summary>
/// Final consistency pass: (1) Business Module protection applies to PUT as well as DELETE;
/// (2) POST record-followup is the single business action and writes exactly one history cycle.
/// </summary>
public class FinalConsistencyTests
{
    private static readonly ICurrentUserService Placeholder = Mock.Of<ICurrentUserService>(u => u.UserId == 0);

    private static BusinessModuleService Modules(EaFmsDbContext db) =>
        new(db, new BusinessModuleRepository(db), Placeholder, Mock.Of<IAuditService>());

    private static FollowupService Followups(EaFmsDbContext db) => new(
        new FollowupRepository(db), db, Mapper, Placeholder, Mock.Of<IAuditService>(), new FollowupSourceResolver(db));

    private static SaveBusinessModuleDto Save(string name, bool active) =>
        new() { Name = name, IsActive = active, EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav" };

    // ---------------- Business Module protection ----------------
    [Theory]
    [InlineData("Meeting")]
    [InlineData("EA Approval")]
    public async Task ProtectedModule_CannotBeDeactivated_ByDelete_OrByPutIsActiveFalse(string name)
    {
        await using var db = MakeDb();
        var svc = Modules(db);
        var m = await svc.CreateAsync(Save(name, true), default);

        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.DeactivateAsync(m.Id, null, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => svc.UpdateAsync(m.Id, Save(name, false), default));

        Assert.True((await svc.GetByIdAsync(m.Id, default)).IsActive);
        Assert.False((await db.BusinessModules.AsNoTracking().SingleAsync()).IsDeleted);
    }

    [Fact]
    public async Task ProtectedModule_PutWithIsActiveTrue_StillWorks()
    {
        await using var db = MakeDb();
        var svc = Modules(db);
        var m = await svc.CreateAsync(Save("Meeting", true), default);

        var updated = await svc.UpdateAsync(m.Id, new SaveBusinessModuleDto { Name = "Meeting", Description = "d", IsActive = true, EmployeeName = "Siddhi Jadhav" }, default);

        Assert.True(updated.IsActive);
        Assert.Equal("d", updated.Description);
    }

    [Fact]
    public async Task OrdinaryModule_DeactivatesAndReactivatesViaPut_WithoutDeletingHistory()
    {
        await using var db = MakeDb();
        var svc = Modules(db);
        var m = await svc.CreateAsync(Save("Travel & Hospitality", true), default);   // not a protected module
        var d = await svc.CreateAsync(Save("Delegation", true), default);
        db.Tasks.Add(new EaTask { BusinessModuleId = m.Id, ModuleName = m.Name, BusinessRecordId = "1", Task = "t", ExecutionStatus = "NotStarted", CreatedBy = "1", CreatedDate = Base });
        db.Followups.Add(new Followup { BusinessModuleId = m.Id, BusinessRecordId = "1", DueAt = Base, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();

        var off = await svc.UpdateAsync(m.Id, Save("Travel & Hospitality", false), default);
        var viaDelete = await svc.DeactivateAsync(d.Id, null, default);
        var on = await svc.UpdateAsync(m.Id, Save("Travel & Hospitality", true), default);

        Assert.False(off.IsActive);
        Assert.False(viaDelete.IsActive);
        Assert.True(on.IsActive);
        Assert.Equal(2, await db.BusinessModules.CountAsync(x => !x.IsDeleted));
        Assert.Equal(1, await db.Tasks.CountAsync());
        Assert.Equal(1, await db.Followups.CountAsync());
    }

    // ---------------- record-followup = one action, one history row ----------------
    private static async Task<(EaFmsDbContext Db, FollowupService Svc, FollowupResponseDto F)> NewFollowupAsync()
    {
        var s = await SeedAsync();
        var svc = Followups(s.Db);
        var f = await svc.CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = s.Modules["Meeting"].Id, BusinessRecordId = s.Meeting.Id.ToString(), DueAt = Base.AddDays(9), Remark = "start",
            ReminderRecipientEmployeeId = "S5I-2000", ReminderRecipientName = "Aman Verma",
            EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav"
        });
        return (s.Db, svc, f);
    }

    [Fact]
    public async Task OneRecordedFollowup_CreatesExactlyOneCycle_WithActorTimeRemarkAndDates()
    {
        var (db, svc, f) = await NewFollowupAsync(); await using var _ = db;
        var next = Base.AddDays(2); var expected = Base.AddDays(3);

        await svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto
        {
            Note = "  Waiting for confirmation  ", NextFollowupAt = next, ExpectedResponseAt = expected, OutcomeCode = " WAITING ",
            EmployeeId = " S5I-1013 ", EmployeeName = "Siddhi Jadhav"
        });

        var c = await db.FollowupCycles.AsNoTracking().SingleAsync();           // exactly one
        Assert.Equal((f.Id, 1), (c.FollowupId, c.SequenceNumber));
        Assert.Equal(("S5I-1013", "Siddhi Jadhav", "Siddhi Jadhav"), (c.FollowedUpByEmployeeId, c.FollowedUpByEmployeeName, c.CreatedBy));
        Assert.InRange(c.FollowedUpAt, DateTime.UtcNow.AddSeconds(-2), DateTime.UtcNow.AddSeconds(2));
        Assert.Equal(("Waiting for confirmation", next, expected, "WAITING"), (c.Note, c.NextFollowupAt, c.ExpectedResponseAt, c.OutcomeCode));
    }

    [Fact]
    public async Task RecordedFollowup_AlsoUpdatesTheCurrentSnapshot_AndKeepsCreatorAndRecipientSeparate()
    {
        var (db, svc, f) = await NewFollowupAsync(); await using var _ = db;

        await svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto
        { Note = "Called again", NextFollowupAt = Base.AddDays(4), EmployeeId = "S5I-3000", EmployeeName = "Richa Shah" });
        var after = await svc.GetByIdAsync(f.Id);
        var cycle = await db.FollowupCycles.AsNoTracking().SingleAsync();

        Assert.Equal(("Called again", Base.AddDays(4)), (after.Remark, after.NextFollowupAt));
        Assert.NotNull(after.LastFollowupAt);
        Assert.Equal(cycle.FollowedUpAt, after.LastFollowupAt);                     // same server timestamp
        Assert.Equal(("S5I-3000", "Richa Shah"), (after.ModifiedByEmployeeId, after.ModifiedByEmployeeName));
        Assert.Equal(("S5I-1013", "Siddhi Jadhav"), (after.CreatedByEmployeeId, after.CreatedByEmployeeName));   // creator not overwritten
        Assert.Equal(("S5I-2000", "Aman Verma"), (after.ReminderRecipientEmployeeId, after.ReminderRecipientName)); // recipient not actor
        Assert.Equal(("S5I-3000", "Richa Shah"), (cycle.FollowedUpByEmployeeId, cycle.FollowedUpByEmployeeName));
    }

    [Fact]
    public async Task RepeatedFollowups_CreateSeparateOrderedHistory_WithTheirOwnActors()
    {
        var (db, svc, f) = await NewFollowupAsync(); await using var _ = db;

        await svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { Note = "Waiting for confirmation", EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav" });
        await svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { Note = "Called again", EmployeeId = "S5I-3000", EmployeeName = "Richa Shah" });
        await svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { Note = "Documents received", EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav" });

        var history = await db.FollowupCycles.AsNoTracking().OrderBy(c => c.SequenceNumber).ToListAsync();
        Assert.Equal(new[] { 1, 2, 3 }, history.Select(c => c.SequenceNumber));
        Assert.Equal(new[] { "Waiting for confirmation", "Called again", "Documents received" }, history.Select(c => c.Note));
        Assert.Equal(new[] { "Siddhi Jadhav", "Richa Shah", "Siddhi Jadhav" }, history.Select(c => c.FollowedUpByEmployeeName));
        Assert.True(history.Zip(history.Skip(1), (a, b) => a.FollowedUpAt <= b.FollowedUpAt).All(x => x));
        Assert.Equal("Documents received", (await svc.GetByIdAsync(f.Id)).Remark);
    }

    [Fact]
    public async Task RecordedFollowup_WithoutNote_KeepsTheExistingRemark_AndStillWritesHistory()
    {
        var (db, svc, f) = await NewFollowupAsync(); await using var _ = db;

        await svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { EmployeeName = "Siddhi Jadhav" });

        Assert.Equal("start", (await svc.GetByIdAsync(f.Id)).Remark);
        Assert.Null((await db.FollowupCycles.AsNoTracking().SingleAsync()).Note);
    }

    [Fact]
    public async Task RecordFollowup_RejectsCompletedMissingAndOversized_WithoutWritingHistory()
    {
        var (db, svc, f) = await NewFollowupAsync(); await using var _ = db;

        await Assert.ThrowsAsync<NotFoundException>(() => svc.RecordFollowupAsync(99999, new RecordFollowupRequestDto()));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { OutcomeCode = new string('x', 101) }));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { EmployeeName = new string('x', 101) }));
        await svc.CompleteAsync(f.Id, new CompleteFollowupRequestDto());
        await Assert.ThrowsAsync<BadRequestException>(() => svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { Note = "late" }));

        Assert.Equal(0, await db.FollowupCycles.CountAsync());
    }

    [Fact]
    public void Contract_RecordFollowupIsDocumentedAsCanonical_AndCyclesAsHistoryOnly()
    {
        Assert.Equal(typeof(RecordFollowupRequestDto).GetProperties().Select(p => p.Name).OrderBy(x => x),
            new[] { "EmployeeId", "EmployeeName", "ExpectedResponseAt", "NextFollowupAt", "Note", "OutcomeCode" });
        Assert.Contains(typeof(Jarvis5.Controllers.FollowupsController).GetMethod("RecordFollowup")!.GetCustomAttributes(),
            a => a is Microsoft.AspNetCore.Mvc.HttpPostAttribute p && p.Template == "{id:long}/record-followup");
    }

    // ---------------- regression ----------------
    [Fact]
    public async Task Update_Complete_WhatsApp_Email_AndEscalation_StillWork_AfterRecordedFollowups()
    {
        var s = await SeedAsync(); await using var db = s.Db;
        var svc = Followups(db);
        var f = await svc.CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = s.Modules["Travel & Hospitality"].Id, BusinessRecordId = s.Travel.Id.ToString(), DueAt = Base.AddDays(3),
            ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientEmail = "aman@example.com",
            ReminderSendWhatsApp = true, ReminderWhatsAppNumber = "9999999999", EmployeeName = "Siddhi Jadhav"
        });
        await svc.RecordFollowupAsync(f.Id, new RecordFollowupRequestDto { Note = "x", EmployeeName = "Siddhi Jadhav" });

        var updated = await svc.UpdateAsync(f.Id, new UpdateFollowupRequestDto
        { Subject = "u", DueAt = Base.AddDays(3), ReminderAt = Base.AddDays(1), ReminderSendEmail = true, ReminderRecipientEmail = "aman@example.com",
          ReminderSendWhatsApp = true, ReminderWhatsAppNumber = "9999999999", EmployeeName = "Riya" });
        var wa = await svc.SendWhatsAppAsync(f.Id);
        var email = await svc.SendEmailAsync(f.Id);
        db.EscalationLevels.Add(new EscalationLevel { Id = 1, Code = "L1", Name = "L1", Level = 1, CreatedBy = "seed", CreatedDate = Base });
        await db.SaveChangesAsync();
        var esc = await new EscalationService(new EscalationRepository(db), db, Mapper, Placeholder, Mock.Of<IAuditService>())
            .CreateAsync(new CreateEscalationRequestDto { FollowupId = f.Id, EscalationLevelId = 1 });
        var done = await svc.CompleteAsync(f.Id, new CompleteFollowupRequestDto());

        Assert.Equal("Riya", updated.ModifiedByEmployeeName);
        Assert.Equal("9999999999", wa.Phone);
        Assert.Equal("aman@example.com", email.Email);
        Assert.Equal(f.Id, esc.FollowupId);
        Assert.True(done.IsCompleted);
        Assert.Equal(1, await db.FollowupCycles.CountAsync());          // none of those actions add history
    }
}
