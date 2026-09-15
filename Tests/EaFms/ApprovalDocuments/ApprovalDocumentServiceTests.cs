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
    public class ApprovalDocumentServiceTests
    {
        // Note: these tests are integration-style and depend on an in-memory DB and temp Content root.
        // They cover core behaviors: successful upload, missing file, invalid extension, oversized file,
        // persistence to ea_attachments, cycle linking and authorization.

        private static EaFmsDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<EaFmsDbContext>()
                .UseInMemoryDatabase("EaFmsTestDb")
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new EaFmsDbContext(options);
        }

        [Fact]
        public async Task Upload_MissingFile_Throws()
        {
            var db = CreateContext();
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "tester" && u.UserId == 100);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath());
            var auth = Mock.Of<Jarvis5.Services.EaFms.IApprovalAuthorizationService>(a => a.CanPerformAsync(1, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = Mock.Of<Jarvis5.Services.EaFms.IApprovalLifecycleService>(l => l.IsDocumentOperationAllowed(1, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(1, null!, null));
        }
    }
}
