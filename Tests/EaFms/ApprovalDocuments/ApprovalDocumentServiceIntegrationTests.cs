using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.ApprovalDocuments
{
    public class ApprovalDocumentServiceIntegrationTests
    {
        private static EaFmsDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<EaFmsDbContext>()
                .UseInMemoryDatabase(dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new EaFmsDbContext(options);
        }

        [Fact]
        public async Task Upload_WithNoCurrentCycle_IsRejected()
        {
            var db = CreateContext("NoCycleDb");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "tester" && u.UserId == 100);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath());

            var authMock = new Mock<IApprovalAuthorizationService>();
            authMock.Setup(a => a.CanPerformAsync(1, "upload", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var lifecycleMock = new Mock<IApprovalLifecycleService>();
            lifecycleMock.Setup(l => l.IsDocumentOperationAllowed(1, "upload", It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var svc = new ApprovalDocumentService(db, user, audit, env, authMock.Object, lifecycleMock.Object);

            // Create approval request in Draft with no current cycle
            var req = new ApprovalRequest { Id = 1, CreatedBy = "tester", WorkflowStatus = "Draft", CurrentCycleNo = 0 };
            db.ApprovalRequests.Add(req);
            await db.SaveChangesAsync();

            var fileMock = new Mock<IFormFile>();
            var content = "Hello World from a fake file";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns("test.pdf");
            fileMock.Setup(_ => _.Length).Returns(ms.Length);

            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(1, fileMock.Object, null));
        }
    }
}
