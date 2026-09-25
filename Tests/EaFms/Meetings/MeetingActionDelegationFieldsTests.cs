using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Controllers;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// Meeting action items carry startDate, assigneeId, assigneeName and delegationType through
/// POST/GET /api/ea/meetings/{meetingId}/actions (and the AI confirm path). A delegationType is
/// validated against the Delegation TAT rules when the action is saved.
/// </summary>
public class MeetingActionDelegationFieldsTests
{
    private static async Task<(EaFmsDbContext Db, long MeetingId, long DelegationModuleId)> NewAsync()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        var meeting = new Meeting { Title = "Board meeting", CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return (db, meeting.Id, module.Id);
    }

    private static void AddRule(EaFmsDbContext db, long moduleId, string type) =>
        db.TatRules.Add(new TatRule { BusinessModuleId = moduleId, ModuleName = "Delegation", Type = type, TaskType = DelegationTaskType.Actual,
            TatMinutes = 60, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });

    [Fact]
    public async Task Post_ThenGet_ReturnsStartDateAssigneeAndDelegationType()
    {
        var (db, meetingId, moduleId) = await NewAsync();
        AddRule(db, moduleId, "Report");
        await db.SaveChangesAsync();
        var controller = new MeetingsActionsController(db);
        var start = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc);

        var post = await controller.Create(meetingId, new CreateMeetingActionDto
        {
            Title = "Send minutes", DoerId = "E-7", DoerName = "Riya", StartDate = start,
            AssigneeId = " E-3 ", AssigneeName = " Ravi ", DelegationType = " Report ",
        }, default);
        Assert.IsType<CreatedAtActionResult>(post);

        var get = Assert.IsType<OkObjectResult>(await controller.Get(meetingId, default));
        var action = Assert.Single(Assert.IsAssignableFrom<IEnumerable<MeetingActionDto>>(get.Value));
        Assert.Equal(start, action.StartDate);
        Assert.Equal(("E-3", "Ravi"), (action.AssigneeId, action.AssigneeName));
        Assert.Equal("Report", action.DelegationType);
        Assert.Equal(("E-7", "Riya"), (action.DoerId, action.DoerName)); // doer unaffected
    }

    [Fact]
    public async Task NewFields_AreOptional()
    {
        var (db, meetingId, _) = await NewAsync();
        var controller = new MeetingsActionsController(db);

        await controller.Create(meetingId, new CreateMeetingActionDto { Title = "t", DelegationType = "  " }, default);

        var row = await db.MeetingActions.SingleAsync();
        Assert.Null(row.StartDate);
        Assert.Null(row.AssigneeId);
        Assert.Null(row.AssigneeName);
        Assert.Null(row.DelegationType);
    }

    [Fact]
    public async Task DelegationType_WithoutATatRule_Is409_AndNothingIsSaved()
    {
        var (db, meetingId, _) = await NewAsync();
        var controller = new MeetingsActionsController(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.Create(meetingId, new CreateMeetingActionDto { Title = "t", DelegationType = "Unknown" }, default));

        Assert.Contains("No active Delegation TAT rule", ex.Message);
        Assert.False(await db.MeetingActions.AnyAsync());
    }

    [Fact]
    public async Task OverlongAssignee_Is400()
    {
        var (db, meetingId, _) = await NewAsync();
        var controller = new MeetingsActionsController(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.Create(meetingId, new CreateMeetingActionDto { Title = "t", AssigneeId = new string('x', 101) }, default));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.Create(meetingId, new CreateMeetingActionDto { Title = "t", AssigneeName = new string('x', 201) }, default));
    }

    // ---- The post-completion delegation flow (MeetingDelegationService.ConfirmAsync) ----

    private static MeetingDelegationService ConfirmService(EaFmsDbContext db)
    {
        var user = Moq.Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "ea" && u.UserId == 1L);
        var tasks = new Moq.Mock<IEaTaskService>();
        EaTaskResponseDto NewTask(CreateEaTaskDto dto, int? tat)
        {
            var module = db.BusinessModules.Single(m => m.Id == dto.ModuleId);
            var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = dto.BusinessRecordId, Task = dto.Task ?? "t",
                Type = dto.Type, AllottedTatMinutes = tat, ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
            db.Tasks.Add(t); db.SaveChanges();
            return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
        }
        tasks.Setup(s => s.CreateWithoutTatAsync(Moq.It.IsAny<CreateEaTaskDto>(), Moq.It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, System.Threading.CancellationToken _) => NewTask(dto, null));
        tasks.Setup(s => s.CreateWithTypeOnlyTatAsync(Moq.It.IsAny<CreateEaTaskDto>(), Moq.It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, System.Threading.CancellationToken _) => NewTask(dto, 60));
        var numbers = new Moq.Mock<Jarvis5.Repositories.EaFms.IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(n => n.GenerateNextReferenceNoAsync(Moq.It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(() => $"DLG-M-{++seq:D6}");
        var audit = Moq.Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
        var delegations = new DelegationService(db, user, audit, numbers.Object, tasks.Object,
            Moq.Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(e => e.ContentRootPath == System.IO.Path.GetTempPath()),
            new TaskReviewService(db, new Jarvis5.Repositories.EaFms.TaskReviewRepository(db), user, audit), new Jarvis5.Repositories.EaFms.TatRuleRepository(db));
        return new MeetingDelegationService(db, delegations, audit);
    }

    private static async Task CompleteMeetingAsync(EaFmsDbContext db, long meetingId)
    {
        db.BusinessModules.Add(new BusinessModule { Name = "Meeting", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        var meeting = await db.Meetings.SingleAsync(m => m.Id == meetingId);
        meeting.MeetingNumber = "MTG-1";
        meeting.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Confirm_CarriesStartDateAssigneeAndDelegationType_IntoTheDelegation()
    {
        var (db, meetingId, moduleId) = await NewAsync();
        AddRule(db, moduleId, "Report");
        await CompleteMeetingAsync(db, meetingId);
        var start = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc);

        var result = await ConfirmService(db).ConfirmAsync(meetingId, new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "Send minutes", DoerId = "E-7", DoerName = "Riya",
                StartDate = start, AssigneeId = "E-3", AssigneeName = "Ravi", DelegationType = "Report" } }
        }, "ea");

        var created = Assert.Single(result.CreatedActions);
        Assert.Equal((start, "E-3", "Ravi", "Report"), (created.StartDate, created.AssigneeId, created.AssigneeName, created.DelegationType));
        var delegation = await db.Delegations.AsNoTracking().SingleAsync(d => d.Id == created.DelegationId);
        Assert.Equal(start, delegation.StartDate);
        Assert.Equal(("E-3", "Ravi"), (delegation.AssigneeId, delegation.AssigneeNameSnapshot));
        Assert.Equal(("E-7", "Riya"), (delegation.DoerId, delegation.DoerNameSnapshot));
        Assert.Equal("Report", delegation.DelegationType);
    }

    [Fact]
    public async Task Confirm_ExistingAction_UpdatesTheNewFields_BeforeDelegating()
    {
        var (db, meetingId, _) = await NewAsync();
        var existing = new MeetingAction { MeetingId = meetingId, Title = "Old", DoerId = "E-7", DoerName = "Riya", CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.MeetingActions.Add(existing);
        await CompleteMeetingAsync(db, meetingId);

        await ConfirmService(db).ConfirmAsync(meetingId, new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { MeetingActionId = existing.Id, Title = "New", DoerId = "E-7", DoerName = "Riya", AssigneeId = "E-9", AssigneeName = "Neha" } }
        }, "ea");

        var row = await db.MeetingActions.AsNoTracking().SingleAsync();
        Assert.Equal(("E-9", "Neha"), (row.AssigneeId, row.AssigneeName));
        Assert.Equal(("E-9", "Neha"), await db.Delegations.AsNoTracking().Select(d => ValueTuple.Create(d.AssigneeId, d.AssigneeNameSnapshot)).SingleAsync());
    }

    [Fact]
    public async Task Confirm_DelegationTypeWithoutATatRule_Is409_AndNothingIsSaved()
    {
        var (db, meetingId, _) = await NewAsync();
        await CompleteMeetingAsync(db, meetingId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => ConfirmService(db).ConfirmAsync(meetingId, new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "t", DoerId = "E-7", DoerName = "Riya", DelegationType = "Unknown" } }
        }, "ea"));

        Assert.Contains("No active Delegation TAT rule", ex.Message);
        Assert.False(await db.MeetingActions.AnyAsync());
        Assert.False(await db.Delegations.AnyAsync());
    }

    [Fact]
    public void AiConfirm_ReusesTheSameRequestShape_SoTheNewFieldsAndValidationApplyThereToo()
    {
        Assert.Equal(typeof(List<CreateMeetingActionDto>), typeof(ConfirmMeetingAiActionsRequestDto).GetProperty("Actions")!.PropertyType);
    }
}
