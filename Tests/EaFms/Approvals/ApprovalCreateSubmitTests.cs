using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Approvals;

/// <summary>
/// Verifies POST create path creates the request + first normal cycle (no Draft / no separate submit).
/// </summary>
public class ApprovalCreateSubmitTests
{
    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<(EaFmsDbContext db, ApprovalService service, Mock<IEaTaskService> eaTasks, long moduleId)> MakeServiceAsync()
    {
        var db = MakeDb();
        var module = new BusinessModule
        {
            Name = "EA Approval",
            IsActive = true,
            IsDeleted = false,
            CreatedBy = "tester",
            CreatedDate = DateTime.UtcNow
        };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();

        var numbers = new Mock<IApprovalNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"APR-{Interlocked.Increment(ref seq):D4}");

        var eaTasks = new Mock<IEaTaskService>();
        eaTasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var task = new EaTask
                {
                    BusinessModuleId = module.Id,
                    ModuleName = module.Name,
                    BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task,
                    Description = dto.Description,
                    AllottedTatMinutes = null,
                    ExecutionStatus = "NotStarted",
                    IsActive = true,
                    CreatedBy = "tester",
                    CreatedDate = DateTime.UtcNow
                };
                db.Tasks.Add(task);
                db.SaveChanges();
                return new EaTaskResponseDto
                {
                    EaTaskId = task.Id,
                    ModuleId = module.Id,
                    ModuleName = module.Name,
                    BusinessRecordId = task.BusinessRecordId,
                    Task = task.Task,
                    Description = task.Description,
                    AllottedTatMinutes = null,
                    ExecutionStatus = task.ExecutionStatus,
                    IsActive = true,
                    CreatedBy = task.CreatedBy,
                    CreatedDate = task.CreatedDate
                };
            });

        var audit = new Mock<IAuditService>();
        var service = new ApprovalService(db, audit.Object, numbers.Object, eaTasks.Object);
        return (db, service, eaTasks, module.Id);
    }

    private static IFormFile FormFile(byte[] data, string fileName) =>
        new FormFile(new MemoryStream(data), 0, data.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

    private static ApprovalDocumentService MakeDocService(EaFmsDbContext db, string contentRoot)
    {
        var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "creator" && u.UserId == 1);
        var audit = Mock.Of<IAuditService>();
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == contentRoot);
        var auth = new ApprovalAuthorizationService(db);
        var lifecycle = new ApprovalLifecycleService(db, audit,
            new TaskReviewService(db, new TaskReviewRepository(db), Mock.Of<Jarvis5.Services.ICurrentUserService>(), audit));
        return new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);
    }

    [Fact]
    public async Task Create_WithoutDocuments_CreatesPendingApprovalAndExactlyOneNormalCycle()
    {
        var (db, service, eaTasks, _) = await MakeServiceAsync();

        var created = await service.CreateAsync(new ApprovalRequest
        {
            RequestTitle = "Capex",
            RequestType = "Finance",
            Department = "Ops",
            CreatedBy = "creator"
        });

        Assert.Equal("PendingApproval", created.WorkflowStatus);
        Assert.NotEqual("Draft", created.WorkflowStatus);
        Assert.Equal(1, created.CurrentCycleNo);
        Assert.NotNull(created.SubmittedAt);

        var cycles = await db.ApprovalCycles.Where(c => c.ApprovalRequestId == created.Id).ToListAsync();
        Assert.Single(cycles);
        Assert.Equal(1, cycles[0].CycleNo);
        Assert.Equal("PendingApproval", cycles[0].Status);

        eaTasks.Verify(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()), Times.Once);
        eaTasks.Verify(s => s.CreateAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithOneDocument_AssociatesAfterCycleExists()
    {
        var (db, service, _, _) = await MakeServiceAsync();
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var created = await service.CreateAsync(new ApprovalRequest
            {
                RequestTitle = "One doc",
                CreatedBy = "creator"
            });

            var docs = MakeDocService(db, temp);
            var dto = await docs.UploadAsync(created.Id, FormFile(new byte[] { 1, 2, 3 }, "a.pdf"), null);

            Assert.Equal(created.Id, dto.ApprovalRequestId);
            var cycleId = Assert.Single(db.ApprovalCycles.Where(c => c.ApprovalRequestId == created.Id)).Id;
            Assert.Equal(cycleId, dto.ApprovalCycleId);
            Assert.Equal(1, await db.Set<Attachment>().CountAsync(a =>
                a.RelatedEntity == "ApprovalRequest" &&
                a.RelatedEntityId == created.Id.ToString() &&
                !a.IsDeleted));
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Create_WithMultipleDocuments_AssociatesAllToFirstCycle()
    {
        var (db, service, _, _) = await MakeServiceAsync();
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        try
        {
            var created = await service.CreateAsync(new ApprovalRequest
            {
                RequestTitle = "Multi doc",
                CreatedBy = "creator"
            });

            var docs = MakeDocService(db, temp);
            await docs.UploadAsync(created.Id, FormFile(new byte[] { 1 }, "one.pdf"), null);
            await docs.UploadAsync(created.Id, FormFile(new byte[] { 2, 3 }, "two.pdf"), null);
            await docs.UploadAsync(created.Id, FormFile(new byte[] { 4, 5, 6 }, "three.pdf"), null);

            Assert.Equal(1, await db.ApprovalCycles.CountAsync(c => c.ApprovalRequestId == created.Id));
            Assert.Equal(3, await db.Set<Attachment>().CountAsync(a =>
                a.RelatedEntity == "ApprovalRequest" &&
                a.RelatedEntityId == created.Id.ToString() &&
                !a.IsDeleted));
            Assert.Equal("PendingApproval", (await db.ApprovalRequests.SingleAsync(r => r.Id == created.Id)).WorkflowStatus);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Create_ApproverCanContinueExistingApproveFlow()
    {
        var (db, service, _, _) = await MakeServiceAsync();
        var created = await service.CreateAsync(new ApprovalRequest
        {
            RequestTitle = "Approve me",
            CreatedBy = "creator",
            ApproverId = "approver-1"
        });

        var lifecycle = new ApprovalLifecycleService(db, Mock.Of<IAuditService>(),
            new TaskReviewService(db, new TaskReviewRepository(db), Mock.Of<Jarvis5.Services.ICurrentUserService>(), Mock.Of<IAuditService>()));
        var approved = await lifecycle.ApproveAsync(created.Id, new ApprovalDecisionDto { Comment = "ok" });

        Assert.Equal("Approved", approved.WorkflowStatus);
        Assert.Equal("Approved", Assert.Single(db.ApprovalCycles.Where(c => c.ApprovalRequestId == created.Id)).Status);
    }
}
