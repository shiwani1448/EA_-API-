using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text.Json;
using Xunit;

namespace Jarvis5.Tests.EaFms.Approvals;

public class ApprovalNoTatQueryTests
{
    [Theory]
    [InlineData(null, true, "Unavailable", 0)]
    [InlineData(30, false, "Unavailable", 0)]
    [InlineData(30, true, "Overdue", 1)]
    public async Task List_detail_and_dashboard_handle_optional_snapshot(int? minutes, bool started, string dueState, int overdueCount)
    {
        await using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var module = new BusinessModule { Name = "EA Approval", CreatedBy = "tester", CreatedDate = DateTime.UtcNow, IsActive = true };
        var task = new EaTask { BusinessModule = module, ModuleName = module.Name, ExecutionStatus = started ? "InProgress" : "NotStarted", BusinessRecordId = "APR-TEST", Task = "No TAT", CreatedBy = "tester", CreatedDate = DateTime.UtcNow.AddDays(-1), AllottedTatMinutes = minutes };
        task.StartedAt = started ? task.CreatedDate : null;
        var approval = new ApprovalRequest { EaTask = task, ReferenceNo = "APR-TEST", CreatedBy = "tester", CreatedAt = task.CreatedDate, WorkflowStatus = "Draft" };
        db.ApprovalRequests.Add(approval);
        await db.SaveChangesAsync();
        var documents = new Mock<IApprovalDocumentService>();
        documents.Setup(x => x.ListAsync(approval.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ApprovalDocumentResponseDto>());
        var taskReview = new TaskReviewService(db, new TaskReviewRepository(db), Mock.Of<ICurrentUserService>(), Mock.Of<IAuditService>());
        var service = new ApprovalQueryService(db, documents.Object, taskReview, new TatRuleRepository(db));

        var list = await service.ListAsync(null, null, null, null, null, null, null, null, null, null, null, 1, 50, default);
        Assert.Equal(dueState, Assert.Single(list.Items).DueState);
        var detail = await service.DetailAsync(approval.Id, default);
        Assert.NotNull(detail);
        Assert.Equal(minutes, detail!.Task.AllottedTatMinutes);
        Assert.Equal(dueState, detail.Task.DueState);
        Assert.Equal(minutes.HasValue && started ? task.StartedAt!.Value.AddMinutes(minutes.Value) : (DateTime?)null, detail.Task.DueDate);
        Assert.Equal(overdueCount, (await service.DashboardAsync(default)).Overdue);
        var overdue = await service.ListAsync(null, null, null, null, null, null, null, null, null, null, "Overdue", 1, 50, default);
        Assert.Equal(overdueCount, overdue.Items.Count);
        var due = await service.ListAsync(null, null, null, null, null, null, null, null, null, null, "Due", 1, 50, default);
        Assert.Empty(due.Items);
        if (!minutes.HasValue)
            Assert.Contains("\"AllottedTatMinutes\":null", JsonSerializer.Serialize(detail.Task));
    }
}
