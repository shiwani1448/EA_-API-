using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>Completion saves evidence and lifecycle state without delegating action items.</summary>
public class MeetingToDelegationTests : IClassFixture<MeetingDelegationDatabase>
{
    private readonly MeetingDelegationDatabase database;
    public MeetingToDelegationTests(MeetingDelegationDatabase database) => this.database = database;
    private EaFmsDbContext MakeRealDb() => database.CreateContext();

    private static async Task<long> ResolveModuleIdAsync(EaFmsDbContext db, string name) =>
        await db.BusinessModules.Where(m => m.Name == name && m.IsActive && !m.IsDeleted).Select(m => m.Id).SingleAsync();

    private static async Task<int> ResolveInProgressStatusIdAsync(EaFmsDbContext db) =>
        await db.Statuses.Where(s => s.Name == "In Progress").Select(s => s.Id).FirstAsync();

    private static Mock<IMeetingCompletionFileStore> MakeFileStoreMock()
    {
        var files = new Mock<IMeetingCompletionFileStore>();
        files.Setup(f => f.ValidateAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        files.Setup(f => f.SaveAsync(It.IsAny<long>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Content/MeetingCompletion/disposable-step5b3-test.pdf");
        return files;
    }

    private static Mock<IWorkflowExecutionService> MakeExecutionMock() =>
        new Mock<IWorkflowExecutionService>().Also(m =>
            m.Setup(x => x.CompleteAsync(It.IsAny<long>(), It.IsAny<CompleteWorkRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CompleteWorkResponseDto { Workflow = new WorkflowResponseDto() }));

    private static Mock<IMeetingService> MakeMeetingServiceMock() =>
        new Mock<IMeetingService>().Also(m =>
            m.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MeetingDetailResponseDto { TatSummary = new MeetingTatSummaryDto() }));

    private static MeetingLifecycleService MakeLifecycleService(
        EaFmsDbContext db, ICurrentUserService user, IAuditService audit,
        out Mock<IMeetingCompletionFileStore> files)
    {
        files = MakeFileStoreMock();
        return new MeetingLifecycleService(
            db, MakeExecutionMock().Object, user, audit, MakeMeetingServiceMock().Object, files.Object,
            NullLogger<MeetingLifecycleService>.Instance);
    }

    private static IFormFile MakeFakePdf() =>
        Mock.Of<IFormFile>(f => f.FileName == "test.pdf");

    /// <summary>
    /// Seeds Meeting + WorkflowInstance (In Progress, TatStarted) + central EaTask, wired
    /// together the same way MeetingService.CreateAsync/StartAsync wire them for real, so
    /// MeetingLifecycleService.CompleteAsync's own preconditions are satisfied.
    /// </summary>
    private static async Task<(Meeting meeting, long meetingModuleId)> SeedInProgressMeetingAsync(
        EaFmsDbContext db, string marker, string actor = "step5b3-ea")
    {
        var meetingModuleId = await ResolveModuleIdAsync(db, "Meeting");
        var inProgressStatusId = await ResolveInProgressStatusIdAsync(db);
        var now = DateTime.UtcNow;

        var meeting = new Meeting
        {
            Title = $"Step 5B-3 test meeting {marker} (disposable Step 5B-3 test)",
            MeetingNumber = $"MTG-5B3-{marker}",
            DoerIds = Array.Empty<string>(), DoerNames = Array.Empty<string>(),
            CreatedBy = actor, CreatedDate = now, IsDeleted = false
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var workflow = new WorkflowInstance
        {
            BusinessModuleId = meetingModuleId,
            BusinessRecordId = meeting.Id.ToString(),
            StatusId = inProgressStatusId,
            StartedAt = now.AddMinutes(-10),
            TatStartedAt = now.AddMinutes(-10),
            IsActive = true,
            CreatedBy = actor, CreatedDate = now
        };
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();

        meeting.WorkflowInstanceId = workflow.Id;
        await db.SaveChangesAsync();

        var eaTask = new EaTask
        {
            BusinessModuleId = meetingModuleId,
            ModuleName = "Meeting",
            BusinessRecordId = meeting.Id.ToString(),
            Task = meeting.Title!,
            ExecutionStatus = "InProgress",
            StartedAt = workflow.TatStartedAt,
            WorkflowInstanceId = workflow.Id,
            IsActive = true,
            CreatedBy = actor, CreatedDate = now
        };
        db.Tasks.Add(eaTask);
        await db.SaveChangesAsync();

        return (meeting, meetingModuleId);
    }

    private static async Task<MeetingAction> AddActionAsync(
        EaFmsDbContext db, long meetingId, string marker,
        string? doerId = "EMP-5B3-1", string? doerName = "Step 5B-3 Doer",
        string? title = null, string? description = "Disposable Step 5B-3 test action",
        string? priority = "High", DateTime? dueDate = null, bool isDeleted = false)
    {
        var action = new MeetingAction
        {
            MeetingId = meetingId,
            Title = title ?? $"Action {marker}",
            Description = description,
            DoerId = doerId,
            DoerName = doerName,
            Priority = priority,
            DueDate = dueDate ?? DateTime.UtcNow.Date.AddDays(5),
            CreatedBy = "step5b3-ea", CreatedDate = DateTime.UtcNow,
            IsDeleted = isDeleted
        };
        db.MeetingActions.Add(action);
        await db.SaveChangesAsync();
        return action;
    }

    private static MeetingCompleteRequestDto MakeCompleteDto(string mom = "Disposable Step 5B-3 test completion MOM.") =>
        new() { CompletionMom = mom, CompletionPdf = MakeFakePdf() };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("EMP1")]
    public async Task Complete_PreservesEvidenceAndCompletesWithoutDelegating(string? doer)
    {
        await using var db = MakeRealDb();
        var (meeting, module) = await SeedInProgressMeetingAsync(db, Guid.NewGuid().ToString("N"));
        var action = await AddActionAsync(db, meeting.Id, "action", doer);
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var service = MakeLifecycleService(db, user, audit, out _);
        await service.CompleteAsync(meeting.Id, MakeCompleteDto(), default);
        await db.Entry(meeting).ReloadAsync();
        Assert.NotNull(meeting.CompletedAt);
        Assert.NotNull(meeting.CompletionMom);
        Assert.NotNull(meeting.CompletionPdfAttachmentId);
        Assert.Null(meeting.DelegationDecision);
        Assert.False(await db.Delegations.AnyAsync(d => d.SourceBusinessModuleId == module && d.SourceEntityId == action.Id.ToString()));
    }
}

file static class MockExtensions
{
    public static Mock<T> Also<T>(this Mock<T> mock, Action<Mock<T>> configure) where T : class
    {
        configure(mock);
        return mock;
    }
}
