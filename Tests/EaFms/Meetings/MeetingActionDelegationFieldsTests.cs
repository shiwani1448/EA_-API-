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

    [Fact]
    public void AiConfirm_ReusesTheSameRequestShape_SoTheNewFieldsAndValidationApplyThereToo()
    {
        Assert.Equal(typeof(List<CreateMeetingActionDto>), typeof(ConfirmMeetingAiActionsRequestDto).GetProperty("Actions")!.PropertyType);
    }
}
