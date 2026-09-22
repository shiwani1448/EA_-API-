using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.TaskReview;

public class TaskReviewServiceTests
{
    [Theory]
    [InlineData(EaTaskExecutionStatus.Completed)]
    [InlineData(EaTaskExecutionStatus.Cancelled)]
    public async Task TerminalTask_CannotSubmit(string status)
    {
        await using var db = NewDb();
        var task = new EaTask { BusinessRecordId = "1", CreatedBy = "seed", ModuleName = "Delegation", BusinessModuleId = 1, Task = "Test", ExecutionStatus = status };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db).SubmitForReviewAsync(task.Id, new()));
        Assert.Empty(await db.TaskReviews.ToListAsync());
    }

    [Fact]
    public async Task MissingTask_IsNotFound()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).SubmitForReviewAsync(999, new()));
    }

    [Fact]
    public async Task Snapshots_History_Batch_AndExecutionFields_ArePreserved()
    {
        await using var db = NewDb();
        var start = DateTime.UtcNow.AddHours(-1);
        var task = new EaTask { BusinessRecordId = "1", CreatedBy = "seed", ModuleName = "Delegation", BusinessModuleId = 1, Task = "Test", ExecutionStatus = EaTaskExecutionStatus.InProgress, StartedAt = start, TatUsedMinutes = 17 };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        var service = Service(db);
        var first = await service.SubmitForReviewAsync(task.Id, new() { ReviewerId = "external", ReviewerName = "Reviewer", SubmittedById = "submitter", SubmittedByName = "Submitter" });
        await service.RequestReworkAsync(task.Id, new() { ReviewedById = "decision", ReviewedByName = "Decision", ReworkRemark = "Change it" });
        var old = await db.TaskReviews.AsNoTracking().SingleAsync();
        var oldJson = System.Text.Json.JsonSerializer.Serialize(old);
        await service.SubmitForReviewAsync(task.Id, new());
        await service.ApproveAsync(task.Id, new() { ReviewRemark = "Done" });
        var history = await service.GetHistoryAsync(task.Id);
        Assert.Equal(new[] { 1, 2 }, history.Select(h => h.ReviewCycleNumber));
        Assert.Equal(oldJson, System.Text.Json.JsonSerializer.Serialize(await db.TaskReviews.AsNoTracking().SingleAsync(r => r.Id == old.Id)));
        Assert.Equal("external", history[0].ReviewerId);
        Assert.Equal("Submitter", history[0].SubmittedByName);
        Assert.Equal("decision", history[0].ReviewedById);
        Assert.Equal(first.SubmittedForReviewAt, history[0].SubmittedForReviewAt);
        Assert.NotNull(history[0].ReviewedAt);
        Assert.Null(history[1].ReviewerId);
        Assert.Null(history[1].ReviewedById);
        Assert.Equal(DateTimeKind.Utc, first.SubmittedForReviewAt!.Value.Kind);
        var batch = await service.BatchGetCurrentAsync(new[] { task.Id, task.Id, 999L });
        Assert.Equal(2, batch.Count);
        Assert.Equal(2, batch[task.Id].ReviewCycleNumber);
        Assert.Null(batch[999].Status);
        Assert.Equal(0, batch[999].ReviewCycleNumber);
        var persisted = await db.Tasks.AsNoTracking().SingleAsync();
        Assert.Equal(EaTaskExecutionStatus.InProgress, persisted.ExecutionStatus);
        Assert.Equal(start, persisted.StartedAt);
        Assert.Null(persisted.CompletedAt);
        Assert.Equal(17, persisted.TatUsedMinutes);
        Assert.Empty(await db.WorkPauses.ToListAsync());
    }

    private static TaskReviewService Service(EaFmsDbContext db) => new(db, new TaskReviewRepository(db), Mock.Of<ICurrentUserService>(), Mock.Of<IAuditService>());
    private static EaFmsDbContext NewDb()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.BusinessModules.Add(new BusinessModule { Id = 1, Name = "Delegation", CreatedBy = "seed", CreatedDate = DateTime.UtcNow });
        db.SaveChanges();
        return db;
    }
}
