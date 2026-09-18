using AutoMapper;
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

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// EA-wide frontend-owned-requiredness cleanup: Priority is a frontend-owned business
/// string. PriorityLevel is optional discovery/reference data only, never a persistence
/// gate — MeetingService.CreateAsync no longer calls PriorityLevel-backed validation
/// (ResolvePriorityAsync was removed in favor of a plain trim). These tests exercise
/// CreateAsync end to end (repository + InMemory EaFmsDbContext), with IWorkflowService/
/// IEaTaskService mocked since they are unrelated collaborators, not part of the
/// priority contract under test.
/// </summary>
public sealed class MeetingPriorityTests
{
    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static IMapper MakeMapperMock()
    {
        var mock = new Mock<IMapper>();
        mock.Setup(m => m.Map<MeetingDetailResponseDto>(It.IsAny<Meeting>()))
            .Returns<Meeting>(meeting => new MeetingDetailResponseDto { Priority = meeting.Priority });
        mock.Setup(m => m.Map<MeetingListItemResponseDto>(It.IsAny<Meeting>()))
            .Returns<Meeting>(meeting => new MeetingListItemResponseDto { MeetingId = meeting.Id });
        return mock.Object;
    }

    private static async Task<MeetingService> MakeServiceAsync(EaFmsDbContext db)
    {
        db.BusinessModules.Add(new BusinessModule { Name = "Meeting", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserId).Returns(1L);
        user.SetupGet(u => u.UserName).Returns("tester");

        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(w => w.GetOrCreateForBusinessRecordAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Returns<long, string, long?, CancellationToken>(async (moduleId, recordId, intakeId, ct) =>
            {
                var wf = new WorkflowInstance { StatusId = 0, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
                db.WorkflowInstances.Add(wf);
                await db.SaveChangesAsync(ct);
                return new WorkflowResponseDto { Id = wf.Id };
            });

        var eaTasks = new Mock<IEaTaskService>();
        eaTasks.Setup(s => s.CreateAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EaTaskResponseDto());

        return new MeetingService(
            new MeetingRepository(db), db, MakeMapperMock(), user.Object,
            new Mock<IAuditService>().Object, workflow.Object, eaTasks.Object);
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Urgent")]
    [InlineData("Anything Selected By Frontend")]
    public async Task Create_ArbitraryPriority_AcceptedAndStoredVerbatim_NoCatalogGate(string priority)
    {
        await using var db = MakeDb();
        var service = await MakeServiceAsync(db);

        var result = await service.CreateAsync(new CreateMeetingRequestDto { Title = "Test", Priority = priority });

        Assert.Equal(priority, result.Priority);
        var saved = await db.Meetings.SingleAsync();
        Assert.Equal(priority, saved.Priority);
    }

    [Fact]
    public async Task Create_PriorityWithSurroundingWhitespace_IsTrimmed_CasingUnchanged()
    {
        await using var db = MakeDb();
        var service = await MakeServiceAsync(db);

        var result = await service.CreateAsync(new CreateMeetingRequestDto { Title = "Test", Priority = "  high  " });

        Assert.Equal("high", result.Priority);
    }

    [Fact]
    public async Task Create_NullPriority_IsAllowed()
    {
        await using var db = MakeDb();
        var service = await MakeServiceAsync(db);

        var result = await service.CreateAsync(new CreateMeetingRequestDto { Title = "Test", Priority = null });

        Assert.Null(result.Priority);
    }
}
