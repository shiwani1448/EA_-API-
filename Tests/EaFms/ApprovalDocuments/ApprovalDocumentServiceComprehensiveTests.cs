using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.ApprovalDocuments
{
    public class ApprovalDocumentServiceComprehensiveTests
    {
        private static EaFmsDbContext CreateContext(string name)
        {
            var options = new DbContextOptionsBuilder<EaFmsDbContext>()
                .UseInMemoryDatabase(name)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new EaFmsDbContext(options);
        }

        private static IFormFile CreateFormFile(byte[] data, string fileName, string contentType)
        {
            var ms = new MemoryStream(data);
            return new FormFile(ms, 0, ms.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
        }

        [Fact]
        public async Task Upload_SuccessfulUpload_PersistsAttachmentAndWritesFileAndAudit()
        {
            var db = CreateContext("upload_success");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "creator" && u.UserId == 100);
            var auditMock = new Mock<Jarvis5.Services.EaFms.IAuditService>();
            auditMock.Setup(a => a.AddAudit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>()));

            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            // Approval request and cycle
            var req = new ApprovalRequest { Id = 1, CreatedBy = "creator", WorkflowStatus = "Draft", CurrentCycleNo = 1 };
            var cycle = new ApprovalCycle { Id = 11, ApprovalRequestId = 1, CycleNo = 1, Status = "PendingApproval" };
            db.ApprovalRequests.Add(req);
            db.ApprovalCycles.Add(cycle);
            await db.SaveChangesAsync();

            var authMock = new Mock<IApprovalAuthorizationService>();
            authMock.Setup(a => a.CanPerformAsync(1, "upload", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var lifecycle = new ApprovalLifecycleService(db, auditMock.Object);
            var svc = new ApprovalDocumentService(db, user, auditMock.Object, env, authMock.Object, lifecycle);

            var content = new byte[] { 1, 2, 3, 4 };
            var file = CreateFormFile(content, "document.pdf", "application/pdf");

            var dto = await svc.UploadAsync(1, file, null);

            // DTO checks
            Assert.NotNull(dto);
            Assert.Equal("document.pdf", dto.OriginalFileName);
            Assert.Equal(1, dto.ApprovalRequestId);
            Assert.Equal(cycle.Id, dto.ApprovalCycleId);
            Assert.StartsWith("/", dto.DownloadUrl);
            Assert.Contains("Content/Approval/1/", dto.DownloadUrl);

            // DB persisted
            var att = db.Set<Attachment>().AsNoTracking().FirstOrDefault(a => a.Id == dto.Id);
            Assert.NotNull(att);
            Assert.Equal("document.pdf", att.OriginalFileName);
            Assert.Equal(content.Length, att.Size);
            Assert.Equal("application/pdf", att.ContentType);
            Assert.Equal("creator", att.UploadedBy);
            Assert.False(string.IsNullOrWhiteSpace(att.Metadata));
            using (var jd = JsonDocument.Parse(att.Metadata!))
            {
                Assert.True(jd.RootElement.TryGetProperty("ApprovalCycleId", out var p));
                Assert.Equal(cycle.Id, p.GetInt64());
            }

            // File exists physically under content root
            var absolute = Path.GetFullPath(Path.Combine(env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(absolute.StartsWith(Path.GetFullPath(Path.Combine(env.ContentRootPath, "Content")), StringComparison.OrdinalIgnoreCase));
            Assert.True(File.Exists(absolute));

            // Audit called
            auditMock.Verify(a => a.AddAudit(It.Is<string>(s => s.Contains("APPROVAL_DOCUMENT_UPLOADED") || s.Contains("APPROVAL")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>()), Times.AtLeastOnce);

            // cleanup
            try { File.Delete(absolute); Directory.Delete(temp, true); } catch { }
        }

        [Fact]
        public async Task Upload_EmptyFile_Throws()
        {
            var db = CreateContext("upload_empty");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "u" && u.UserId == 1);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(1, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = Mock.Of<IApprovalLifecycleService>(l => l.IsDocumentOperationAllowed(1, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

            var empty = new MemoryStream();
            var file = new FormFile(empty, 0, 0, "file", "empty.pdf") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };

            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(1, file, null));
        }

        [Fact]
        public async Task Upload_InvalidExtension_Throws()
        {
            var db = CreateContext("upload_invalid_ext");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "u" && u.UserId == 2);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(1, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = Mock.Of<IApprovalLifecycleService>(l => l.IsDocumentOperationAllowed(1, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

            var bytes = new byte[] { 1 };
            var file = CreateFormFile(bytes, "payload.exe", "application/octet-stream");

            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(1, file, null));
        }

        [Fact]
        public async Task Upload_OversizedFile_Throws()
        {
            var db = CreateContext("upload_oversized");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "u" && u.UserId == 3);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(1, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = Mock.Of<IApprovalLifecycleService>(l => l.IsDocumentOperationAllowed(1, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

            // create >25MB
            var large = new byte[25 * 1024 * 1024 + 1];
            var file = CreateFormFile(large, "big.pdf", "application/pdf");

            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(1, file, null));
        }

        [Fact]
        public async Task Upload_PathTraversalClientFilename_IsSanitizedAndStoredSafely()
        {
            var db = CreateContext("upload_traversal");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "creator2" && u.UserId == 200);
            var auditMock = new Mock<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var req = new ApprovalRequest { Id = 5, CreatedBy = "creator2", WorkflowStatus = "Draft", CurrentCycleNo = 1 };
            var cycle = new ApprovalCycle { Id = 51, ApprovalRequestId = 5, CycleNo = 1, Status = "PendingApproval" };
            db.ApprovalRequests.Add(req); db.ApprovalCycles.Add(cycle); await db.SaveChangesAsync();

            var authMock = new Mock<IApprovalAuthorizationService>(); authMock.Setup(a => a.CanPerformAsync(5, "upload", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var lifecycle = new ApprovalLifecycleService(db, auditMock.Object);
            var svc = new ApprovalDocumentService(db, user, auditMock.Object, env, authMock.Object, lifecycle);

            var bytes = new byte[] { 9, 9 };
            var file = CreateFormFile(bytes, "..\\..\\secret.pdf", "application/pdf");

            var dto = await svc.UploadAsync(5, file, null);
            Assert.Contains("Content/Approval/5/", dto.DownloadUrl);

            var att = db.Set<Attachment>().AsNoTracking().First(a => a.Id == dto.Id);
            Assert.Equal(Path.GetFileName(file.FileName), att.OriginalFileName); // sanitized by Path.GetFileName

            // stored object key is GUID-based
            var objectFileName = att.ObjectKey.Split('/').Last();
            Assert.Matches("^[0-9a-fA-F]{32}\\.pdf$", objectFileName);

            // cleanup
            try { var absolute = Path.GetFullPath(Path.Combine(env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar))); File.Delete(absolute); Directory.Delete(temp, true); } catch { }
        }

        [Fact]
        public async Task CycleLinking_ExplicitValidCurrentCycle_AllowsUpload()
        {
            var db = CreateContext("cycle_explicit_valid");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "cuser" && u.UserId == 300);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var req = new ApprovalRequest { Id = 7, CreatedBy = "cuser", WorkflowStatus = "Draft", CurrentCycleNo = 2 };
            var c1 = new ApprovalCycle { Id = 71, ApprovalRequestId = 7, CycleNo = 1, Status = "PendingApproval" };
            var c2 = new ApprovalCycle { Id = 72, ApprovalRequestId = 7, CycleNo = 2, Status = "PendingApproval" };
            db.ApprovalRequests.Add(req); db.ApprovalCycles.AddRange(c1, c2); await db.SaveChangesAsync();

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(7, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = new ApprovalLifecycleService(db, Mock.Of<Jarvis5.Services.EaFms.IAuditService>());
            var svc = new ApprovalDocumentService(db, user, Mock.Of<Jarvis5.Services.EaFms.IAuditService>(), env, auth, lifecycle);

            var file = CreateFormFile(new byte[] { 1 }, "ok.pdf", "application/pdf");
            var dto = await svc.UploadAsync(7, file, 72);
            Assert.Equal(72, dto.ApprovalCycleId);

            // cleanup
            var att = db.Set<Attachment>().AsNoTracking().First(a => a.Id == dto.Id);
            try { var absolute = Path.GetFullPath(Path.Combine(env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar))); File.Delete(absolute); Directory.Delete(temp, true); } catch { }
        }

        [Fact]
        public async Task CycleLinking_ExplicitCycleBelongingToAnotherRequest_IsRejected()
        {
            var db = CreateContext("cycle_other_request");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "x" && u.UserId == 400);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            db.ApprovalRequests.Add(new ApprovalRequest { Id = 8, CreatedBy = "x", WorkflowStatus = "Draft", CurrentCycleNo = 1 });
            db.ApprovalRequests.Add(new ApprovalRequest { Id = 9, CreatedBy = "y", WorkflowStatus = "Draft", CurrentCycleNo = 1 });
            var cycle = new ApprovalCycle { Id = 81, ApprovalRequestId = 9, CycleNo = 1, Status = "PendingApproval" };
            db.ApprovalCycles.Add(cycle); await db.SaveChangesAsync();

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(8, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = Mock.Of<IApprovalLifecycleService>(l => l.IsDocumentOperationAllowed(8, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

            var file = CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf");
            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(8, file, 81));
        }

        [Fact]
        public async Task CycleLinking_ExplicitNonCurrentCycle_IsRejected()
        {
            var db = CreateContext("cycle_non_current");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "z" && u.UserId == 500);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var req = new ApprovalRequest { Id = 10, CreatedBy = "z", WorkflowStatus = "Draft", CurrentCycleNo = 2 };
            var c1 = new ApprovalCycle { Id = 101, ApprovalRequestId = 10, CycleNo = 1, Status = "PendingApproval" };
            var c2 = new ApprovalCycle { Id = 102, ApprovalRequestId = 10, CycleNo = 2, Status = "PendingApproval" };
            db.ApprovalRequests.Add(req); db.ApprovalCycles.AddRange(c1, c2); await db.SaveChangesAsync();

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(10, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = Mock.Of<IApprovalLifecycleService>(l => l.IsDocumentOperationAllowed(10, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

            var file = CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf");
            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(10, file, 101));
        }

        [Fact]
        public async Task CycleLinking_OmittedApprovalCycleId_ResolvesCurrentCycleNo()
        {
            var db = CreateContext("cycle_omitted");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "om" && u.UserId == 600);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var req = new ApprovalRequest { Id = 11, CreatedBy = "om", WorkflowStatus = "Draft", CurrentCycleNo = 3 };
            var c3 = new ApprovalCycle { Id = 113, ApprovalRequestId = 11, CycleNo = 3, Status = "PendingApproval" };
            db.ApprovalRequests.Add(req); db.ApprovalCycles.Add(c3); await db.SaveChangesAsync();

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(11, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = new ApprovalLifecycleService(db, audit);
            var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

            var file = CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf");
            var dto = await svc.UploadAsync(11, file, null);
            Assert.Equal(c3.Id, dto.ApprovalCycleId);

            // cleanup
            var att = db.Set<Attachment>().AsNoTracking().First(a => a.Id == dto.Id);
            try { var absolute = Path.GetFullPath(Path.Combine(env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar))); File.Delete(absolute); Directory.Delete(temp, true); } catch { }
        }

        [Fact]
        public async Task CycleLinking_MissingCurrentCycle_IsRejected()
        {
            // Existing test Upload_NoCurrentCycle_Throws covers this; keep here for completeness
            var db = CreateContext("cycle_missing");
            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "m" && u.UserId == 700);
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            db.ApprovalRequests.Add(new ApprovalRequest { Id = 12, CreatedBy = "m", WorkflowStatus = "Draft", CurrentCycleNo = 0 }); await db.SaveChangesAsync();

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(12, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = Mock.Of<IApprovalLifecycleService>(l => l.IsDocumentOperationAllowed(12, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(false));
            var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

            var file = CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf");
            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(12, file, null));
        }

        [Fact(Skip = "Requires simulation of concurrent change of current cycle inside DB transaction; environment does not provide easy hook. Marked as blocker.")]
        public void CycleLinking_CurrentCycleChangeDuringTransaction_IsRejected()
        {
            // Blocker: simulation requires hooking into DbContext.Database.BeginTransactionAsync to mutate rows between pre-check and in-transaction revalidation.
            // This test is intentionally skipped and must be implemented using a test double for DbContext/DatabaseFacade or by introducing an explicit test hook in the service.
        }

        [Fact]
        public async Task Lifecycle_UploadAllowedAndDeniedByWorkflowStatus()
        {
            // Allowed statuses
            var allowed = new[] { "Draft", "PendingApproval", "ChangesRequested", "Resubmitted" };
            foreach (var status in allowed)
            {
                var db = CreateContext("life_ok_" + status);
                var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "lu" && u.UserId == 900);
                var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
                var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);
                var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();

                var req = new ApprovalRequest { Id = 20, CreatedBy = "lu", WorkflowStatus = status, CurrentCycleNo = 1 };
                var cycle = new ApprovalCycle { Id = 201, ApprovalRequestId = 20, CycleNo = 1, Status = status };
                db.ApprovalRequests.Add(req); db.ApprovalCycles.Add(cycle); await db.SaveChangesAsync();

                var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(20, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
                var lifecycle = new ApprovalLifecycleService(db, audit);
                var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

                var file = CreateFormFile(new byte[] { 1 }, "ok.pdf", "application/pdf");
                var dto = await svc.UploadAsync(20, file, null);
                Assert.Equal(cycle.Id, dto.ApprovalCycleId);

                // cleanup
                var att = db.Set<Attachment>().AsNoTracking().First(a => a.Id == dto.Id);
                try { var absolute = Path.GetFullPath(Path.Combine(env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar))); File.Delete(absolute); Directory.Delete(temp, true); } catch { }
            }

            // Denied statuses
            var denied = new[] { "Approved", "Rejected", string.Empty };
            foreach (var status in denied)
            {
                var db = CreateContext("life_denied_" + (string.IsNullOrEmpty(status) ? "empty" : status));
                var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "ld" && u.UserId == 901);
                var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
                var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);
                var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();

                var req = new ApprovalRequest { Id = 30, CreatedBy = "ld", WorkflowStatus = status, CurrentCycleNo = 1 };
                var cycle = new ApprovalCycle { Id = 301, ApprovalRequestId = 30, CycleNo = 1, Status = status };
                db.ApprovalRequests.Add(req); db.ApprovalCycles.Add(cycle); await db.SaveChangesAsync();

                var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(30, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
                var lifecycle = new ApprovalLifecycleService(db, audit);
                var svc = new ApprovalDocumentService(db, user, audit, env, auth, lifecycle);

                var file = CreateFormFile(new byte[] { 1 }, "ok.pdf", "application/pdf");
                await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.UploadAsync(30, file, null));
            }
        }

        [Fact]
        public async Task Authorization_CreatorApproverAndUnauthorizedBehaviors()
        {
            // Creator authorized
            var db = CreateContext("auth_creator");
            var audit = Mock.Of<Jarvis5.Services.EaFms.IAuditService>();
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var req = new ApprovalRequest { Id = 40, EaTaskId = 400, CreatedBy = "alice", WorkflowStatus = "Draft", CurrentCycleNo = 1 };
            var cycle = new ApprovalCycle { Id = 401, ApprovalRequestId = 40, CycleNo = 1, Status = "PendingApproval" };
            db.Tasks.Add(new EaTask { Id = 400, BusinessModuleId = 1, BusinessRecordId = "40", Task = "approval", AllottedTatMinutes = 60, CreatedBy = "alice", CreatedDate = DateTime.UtcNow });
            db.ApprovalRequests.Add(req); db.ApprovalCycles.Add(cycle); await db.SaveChangesAsync();

            var alice = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "alice" && u.UserId == 1000);
            var auth = new ApprovalAuthorizationService(db);
            var lifecycle = new ApprovalLifecycleService(db, audit);
            var svc = new ApprovalDocumentService(db, alice, audit, env, auth, lifecycle);
            var file = CreateFormFile(new byte[] { 1 }, "a.pdf", "application/pdf");
            var dto = await svc.UploadAsync(40, file, null);
            Assert.Equal(401, dto.ApprovalCycleId);

            // Assigned approver by ApproverId
            var db2 = CreateContext("auth_approver");
            var req2 = new ApprovalRequest { Id = 41, EaTaskId = 410, CreatedBy = "bob", WorkflowStatus = "Draft", CurrentCycleNo = 1, ApproverId = "2000", ApproverName = "approver-display" };
            var cycle2 = new ApprovalCycle { Id = 411, ApprovalRequestId = 41, CycleNo = 1, Status = "PendingApproval" };
            db2.Tasks.Add(new EaTask { Id = 410, BusinessModuleId = 1, BusinessRecordId = "41", Task = "approval", AllottedTatMinutes = 60, CreatedBy = "bob", CreatedDate = DateTime.UtcNow });
            db2.ApprovalRequests.Add(req2); db2.ApprovalCycles.Add(cycle2); await db2.SaveChangesAsync();

            var approver = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "someone" && u.UserId == 2000);
            var auth2 = new ApprovalAuthorizationService(db2);
            var lifecycle2 = new ApprovalLifecycleService(db2, audit);
            var svc2 = new ApprovalDocumentService(db2, approver, audit, env, auth2, lifecycle2);
            var dto2 = await svc2.UploadAsync(41, file, null);
            Assert.Equal(411, dto2.ApprovalCycleId);

            // Existence-based auth: inactive linked EA task is denied (identity is not evaluated).
            var db3 = CreateContext("auth_inactive_task");
            var req3 = new ApprovalRequest { Id = 42, EaTaskId = 420, CreatedBy = "charlie", WorkflowStatus = "Draft", CurrentCycleNo = 1, ApproverId = "3000", ApproverName = "display-name" };
            var cycle3 = new ApprovalCycle { Id = 421, ApprovalRequestId = 42, CycleNo = 1, Status = "PendingApproval" };
            db3.Tasks.Add(new EaTask { Id = 420, BusinessModuleId = 1, BusinessRecordId = "42", Task = "approval", AllottedTatMinutes = 60, CreatedBy = "charlie", CreatedDate = DateTime.UtcNow, IsActive = false });
            db3.ApprovalRequests.Add(req3); db3.ApprovalCycles.Add(cycle3); await db3.SaveChangesAsync();

            var userMatchingDisplay = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "display-name" && u.UserId == 9999);
            var auth3 = new ApprovalAuthorizationService(db3);
            var lifecycle3 = new ApprovalLifecycleService(db3, audit);
            var svc3 = new ApprovalDocumentService(db3, userMatchingDisplay, audit, env, auth3, lifecycle3);

            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc3.UploadAsync(42, file, null));
        }

        [Fact]
        public async Task Delete_SoftDeleteAndExclusionAndAudit()
        {
            var db = CreateContext("delete_flow");
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);
            var auditMock = new Mock<Jarvis5.Services.EaFms.IAuditService>();
            auditMock.Setup(a => a.AddAudit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>()));

            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "deleter" && u.UserId == 4000);
            var req = new ApprovalRequest { Id = 50, CreatedBy = "deleter", WorkflowStatus = "Draft", CurrentCycleNo = 1 };
            var cycle = new ApprovalCycle { Id = 501, ApprovalRequestId = 50, CycleNo = 1, Status = "Draft" };
            db.ApprovalRequests.Add(req); db.ApprovalCycles.Add(cycle); await db.SaveChangesAsync();

            var authMock = new Mock<IApprovalAuthorizationService>();
            authMock.Setup(a => a.CanPerformAsync(50, "upload", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            authMock.Setup(a => a.CanPerformAsync(50, "delete", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            authMock.Setup(a => a.CanPerformAsync(50, "list", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var lifecycleMock = new Mock<IApprovalLifecycleService>();
            lifecycleMock.Setup(l => l.IsDocumentOperationAllowed(50, "upload", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            lifecycleMock.Setup(l => l.IsDocumentOperationAllowed(50, "delete", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var svc = new ApprovalDocumentService(db, user, auditMock.Object, env, authMock.Object, lifecycleMock.Object);

            // upload
            var file = CreateFormFile(new byte[] { 9 }, "d.pdf", "application/pdf");
            var dto = await svc.UploadAsync(50, file, null);
            var att = db.Set<Attachment>().First(a => a.Id == dto.Id);
            var absolute = Path.GetFullPath(Path.Combine(env.ContentRootPath, att.ObjectKey.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(File.Exists(absolute));

            // delete
            await svc.DeleteAsync(50, dto.Id);
            var deleted = db.Set<Attachment>().AsNoTracking().First(a => a.Id == dto.Id);
            Assert.True(deleted.IsDeleted);
            Assert.False(deleted.IsActive);

            // list excludes deleted
            var list = await svc.ListAsync(50);
            Assert.DoesNotContain(list, x => x.Id == dto.Id);

            // download rejected
            await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => svc.DownloadAsync(50, dto.Id));

            auditMock.Verify(a => a.AddAudit(It.Is<string>(s => s.Contains("APPROVAL_DOCUMENT_REMOVED") || s.Contains("REMOVED")), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>()), Times.AtLeastOnce);

            // cleanup
            try { if (File.Exists(absolute)) File.Delete(absolute); Directory.Delete(temp, true); } catch { }
        }

        [Fact]
        public async Task AuditAndFailureCleanup_FileDeletedOnPersistenceFailure()
        {
            var db = CreateContext("failure_cleanup");
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
            var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == temp);

            var user = Mock.Of<Jarvis5.Services.ICurrentUserService>(u => u.UserName == "fc" && u.UserId == 5000);
            var req = new ApprovalRequest { Id = 60, CreatedBy = "fc", WorkflowStatus = "Draft", CurrentCycleNo = 1 };
            var cycle = new ApprovalCycle { Id = 601, ApprovalRequestId = 60, CycleNo = 1, Status = "PendingApproval" };
            db.ApprovalRequests.Add(req); db.ApprovalCycles.Add(cycle); await db.SaveChangesAsync();

            var auditMock = new Mock<Jarvis5.Services.EaFms.IAuditService>();
            // Simulate DB/audit failure by throwing when AddAudit is called
            auditMock.Setup(a => a.AddAudit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>())).Throws(new Exception("audit failure"));

            var auth = Mock.Of<IApprovalAuthorizationService>(a => a.CanPerformAsync(60, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var lifecycle = Mock.Of<IApprovalLifecycleService>(l => l.IsDocumentOperationAllowed(60, "upload", It.IsAny<CancellationToken>()) == Task.FromResult(true));
            var svc = new ApprovalDocumentService(db, user, auditMock.Object, env, auth, lifecycle);

            var file = CreateFormFile(new byte[] { 1, 2, 3 }, "f.pdf", "application/pdf");

            await Assert.ThrowsAsync<Exception>(() => svc.UploadAsync(60, file, null));

            // Ensure no file remains
            var files = Directory.GetFiles(Path.Combine(temp, "Content"), "*", SearchOption.AllDirectories);
            Assert.Empty(files);
        }

        [Fact]
        public void Architecture_NoProhibitedApprovalSpecificEntities()
        {
            var forbidden = new[] { "ApprovalDocument", "ApprovalHistory", "ApprovalReminder", "ApprovalEscalation" };
            var props = typeof(EaFmsDbContext).GetProperties().Select(p => p.Name).ToArray();
            foreach (var f in forbidden)
            {
                Assert.DoesNotContain(props, p => p.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0);
            }
        }
    }
}
