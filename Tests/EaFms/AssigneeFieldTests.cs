using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms;

/// <summary>
/// Delegation has an explicit, optional Assignee — a separate person from the Doer — sent in the
/// create/update request exactly like the Doer and returned by create, GET and the list.
/// Meeting has no Assignee field.
/// </summary>
public class AssigneeFieldTests
{
    private static async Task<(EaFmsDbContext Db, DelegationService Svc)> NewAsync()
    {
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 42L);
        var numbers = new Mock<IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"DLG-A-{++seq:D6}");
        var tasks = new Mock<IEaTaskService>();
        tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task ?? "t", ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
            });
        var audit = new Mock<IAuditService>();
        var svc = new DelegationService(db, user, audit.Object, numbers.Object, tasks.Object,
            Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath()),
            new TaskReviewService(db, new TaskReviewRepository(db), user, audit.Object), new TatRuleRepository(db));
        return (db, svc);
    }

    [Fact]
    public async Task Create_StoresAssigneeSeparatelyFromDoer_AndGetAndListReturnIt()
    {
        var (db, svc) = await NewAsync();

        var created = await svc.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Prepare deck", DoerId = "E-7", DoerNameSnapshot = "Riya",
            AssigneeId = "  E-3 ", AssigneeName = " Ravi ", EndDate = DateTime.UtcNow.AddDays(3)
        });

        Assert.Equal(("E-3", "Ravi"), (created.AssigneeId, created.AssigneeName));
        Assert.Equal(("E-7", "Riya"), (created.DoerId, created.DoerName));            // doer unaffected
        var row = await db.Delegations.AsNoTracking().SingleAsync();
        Assert.Equal(("E-3", "Ravi"), (row.AssigneeId, row.AssigneeNameSnapshot));

        var got = await svc.GetByIdAsync(created.DelegationId);
        Assert.Equal(("E-3", "Ravi"), (got.AssigneeId, got.AssigneeName));
        var listed = (await svc.ListAsync(new DelegationListQueryDto())).Items.Single();
        Assert.Equal(("E-3", "Ravi"), (listed.AssigneeId, listed.AssigneeName));
    }

    [Fact]
    public async Task Assignee_IsOptional_AndUpdateCanSetAndClearIt()
    {
        var (_, svc) = await NewAsync();
        var created = await svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "E-7", EndDate = DateTime.UtcNow.AddDays(3) });
        Assert.Null(created.AssigneeId);
        Assert.Null(created.AssigneeName);

        var set = await svc.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto
            { Title = "t", DoerId = "E-7", AssigneeId = "E-9", AssigneeName = "Neha", EndDate = DateTime.UtcNow.AddDays(3) });
        Assert.Equal(("E-9", "Neha"), (set.AssigneeId, set.AssigneeName));

        var cleared = await svc.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto
            { Title = "t", DoerId = "E-7", AssigneeId = " ", EndDate = DateTime.UtcNow.AddDays(3) });
        Assert.Null(cleared.AssigneeId);
        Assert.Null(cleared.AssigneeName);
    }

    [Fact]
    public void Json_UsesAssigneeNames_InRequestAndResponse_AndMeetingHasNoAssignee()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var request = JsonSerializer.Deserialize<DelegationCreateRequestDto>("""{"assigneeId":"E-3","assigneeName":"Ravi"}""", web)!;
        Assert.Equal(("E-3", "Ravi"), (request.AssigneeId, request.AssigneeName));

        var response = JsonSerializer.SerializeToElement(new DelegationResponseDto { AssigneeId = "E-3", AssigneeName = "Ravi" }, web);
        Assert.Equal("E-3", response.GetProperty("assigneeId").GetString());
        Assert.Equal("Ravi", response.GetProperty("assigneeName").GetString());

        foreach (var t in new[] { typeof(MeetingListItemResponseDto), typeof(MeetingDetailResponseDto), typeof(CreateMeetingRequestDto), typeof(UpdateMeetingRequestDto) })
            Assert.DoesNotContain(t.GetProperties(), p => p.Name.Contains("Assignee"));
    }
}
