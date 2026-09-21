using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Controllers.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Filters;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Moq;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Jarvis5.Tests.EaFms.Approvals;

/// <summary>
/// ApprovedBy / RejectedBy on EA Approval (and the neutral Swagger example for both Travel and Approval
/// create contracts). ApproverName = designated approver; ApprovedBy / RejectedBy = who actually decided.
/// </summary>
public class ApprovalApprovedRejectedByTests
{
    private sealed class Fx : IAsyncDisposable
    {
        public EaFmsDbContext Db { get; } = new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        public ApprovalService Service { get; private set; } = null!;
        public ApprovalsController Controller { get; private set; } = null!;
        public ApprovalQueryService Queries { get; private set; } = null!;
        public ApprovalLifecycleService Lifecycle { get; private set; } = null!;

        public static async Task<Fx> CreateAsync()
        {
            var f = new Fx();
            var module = new BusinessModule { Name = "EA Approval", IsActive = true, CreatedBy = "t", CreatedDate = DateTime.UtcNow };
            f.Db.BusinessModules.Add(module);
            await f.Db.SaveChangesAsync();
            var numbers = new Mock<IApprovalNumberRepository>();
            var n = 0;
            numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"APR-AB-{++n:D4}");
            var tasks = new Mock<IEaTaskService>();
            tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateEaTaskDto d, CancellationToken _) =>
                {
                    var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = d.BusinessRecordId, Task = d.Task,
                        ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "t", CreatedDate = DateTime.UtcNow };
                    f.Db.Tasks.Add(t); f.Db.SaveChanges();
                    return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = t.BusinessRecordId,
                        Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = "t", CreatedDate = t.CreatedDate };
                });
            var audit = Mock.Of<IAuditService>();
            f.Service = new ApprovalService(f.Db, audit, numbers.Object, tasks.Object);
            f.Queries = new ApprovalQueryService(f.Db, Mock.Of<IApprovalDocumentService>());
            f.Controller = new ApprovalsController(f.Service, f.Queries);
            f.Lifecycle = new ApprovalLifecycleService(f.Db, audit);
            return f;
        }

        public async Task<long> PostAsync(ApprovalRequestDto dto)
        {
            var created = Assert.IsType<CreatedAtActionResult>(await Controller.Create(dto, default));
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(created.Value));
            return doc.RootElement.GetProperty("approvalRequestId").GetInt64();
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static ApprovalRequestDto Dto(Action<ApprovalRequestDto>? tweak = null)
    {
        var d = new ApprovalRequestDto { RequestTitle = "Laptop", ApproverId = "dir-1", ApproverName = "Director Rao", CreatedBy = "creator" };
        tweak?.Invoke(d);
        return d;
    }

    [Fact]
    public async Task Post_AcceptsApprovedByAndRejectedBy_PersistsThem_AndReturnsThemOnPostDetailAndList()
    {
        await using var f = await Fx.CreateAsync();

        var created = Assert.IsType<CreatedAtActionResult>(await f.Controller.Create(Dto(d => { d.ApprovedBy = "  Imported Approver "; d.RejectedBy = "Imported Rejecter"; }), default));
        using var post = JsonDocument.Parse(JsonSerializer.Serialize(created.Value));
        var id = post.RootElement.GetProperty("approvalRequestId").GetInt64();
        var row = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync();
        var detail = (await f.Queries.DetailAsync(id, default))!;
        var listed = Assert.Single((await f.Queries.ListAsync(null, null, null, null, null, null, null, null, null, null, null, 1, 50, default)).Items);

        Assert.Equal(("Imported Approver", "Imported Rejecter"), (row.ApprovedBy, row.RejectedBy));
        Assert.Equal("Imported Approver", post.RootElement.GetProperty("approvedBy").GetString());
        Assert.Equal("Imported Rejecter", post.RootElement.GetProperty("rejectedBy").GetString());
        Assert.Equal(("Imported Approver", "Imported Rejecter"), (detail.ApprovedBy, detail.RejectedBy));
        Assert.Equal(("Imported Approver", "Imported Rejecter"), (listed.ApprovedBy, listed.RejectedBy));
    }

    [Fact]
    public async Task Post_WithBothNull_StillWorks_AndHistoricalNullRowsReadFine()
    {
        await using var f = await Fx.CreateAsync();

        var id = await f.PostAsync(Dto());
        var detail = (await f.Queries.DetailAsync(id, default))!;

        Assert.Null(detail.ApprovedBy);
        Assert.Null(detail.RejectedBy);
        Assert.Equal("PendingApproval", detail.WorkflowStatus);
        Assert.Equal("APR-AB-0001", detail.ReferenceNo);
    }

    [Fact]
    public async Task ApproverName_StaysTheDesignatedApprover_SeparateFromApprovedBy()
    {
        await using var f = await Fx.CreateAsync();

        var id = await f.PostAsync(Dto(d => d.ApprovedBy = "Someone Else"));
        var detail = (await f.Queries.DetailAsync(id, default))!;

        Assert.Equal("Director Rao", detail.Approver);           // designated approver
        Assert.Equal("Someone Else", detail.ApprovedBy);         // who actually approved
    }

    [Fact]
    public async Task ApproveAction_SetsApprovedByToTheActor_ClearsRejectedBy_AndKeepsTheDesignatedApprover()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto(d => d.RejectedBy = "stale"));

        var approved = await f.Lifecycle.ApproveAsync(id, new ApprovalDecisionDto { Comment = "ok", EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav" });

        Assert.Equal("Approved", approved.WorkflowStatus);
        Assert.Equal(("Siddhi Jadhav", null, "Director Rao"), (approved.ApprovedBy, approved.RejectedBy, approved.ApproverName));
        Assert.Equal("Siddhi Jadhav", (await f.Queries.DetailAsync(id, default))!.ApprovedBy);
    }

    [Fact]
    public async Task RejectAction_SetsRejectedByToTheActor_ClearsApprovedBy()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto(d => d.ApprovedBy = "stale"));

        var rejected = await f.Lifecycle.RejectAsync(id, new ApprovalDecisionDto { Comment = "no budget", EmployeeName = "Richa Shah" });

        Assert.Equal("Rejected", rejected.WorkflowStatus);
        Assert.Equal(("Richa Shah", null, "Director Rao"), (rejected.RejectedBy, rejected.ApprovedBy, rejected.ApproverName));
    }

    [Fact]
    public async Task Decision_WithoutActor_LeavesSuppliedValuesAlone_AndNeverBorrowsTheDesignatedApprover()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto());

        var approved = await f.Lifecycle.ApproveAsync(id, new ApprovalDecisionDto { Comment = "ok" });

        Assert.Equal("Approved", approved.WorkflowStatus);       // lifecycle unchanged
        Assert.Null(approved.ApprovedBy);                        // ApproverName is not silently reinterpreted
        Assert.Equal("Director Rao", approved.ApproverName);
    }

    // ---------------- ApprovedAt / RejectedAt (decision timestamps) ----------------
    [Fact]
    public async Task PendingApproval_HasNoDecisionTimestamps_EvenWhenApprovedByWasSuppliedAtPost()
    {
        await using var f = await Fx.CreateAsync();

        var id = await f.PostAsync(Dto(d => d.ApprovedBy = "Imported Approver"));
        var detail = (await f.Queries.DetailAsync(id, default))!;

        Assert.Equal("PendingApproval", detail.WorkflowStatus);
        Assert.Equal("Imported Approver", detail.ApprovedBy);       // supplied business data is preserved...
        Assert.Null(detail.ApprovedAt);                             // ...but is not proof of approval
        Assert.Null(detail.RejectedAt);
    }

    [Fact]
    public async Task DraftSave_DoesNotSetApprovedAt()
    {
        await using var f = await Fx.CreateAsync();
        f.Db.ApprovalRequests.Add(new ApprovalRequest { Id = 901, EaTaskId = 1, ReferenceNo = "APR-D2", WorkflowStatus = "Draft", CreatedBy = "c", CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();

        await f.Controller.SaveDraft(901, Dto(d => d.ApprovedBy = "Draft A"), default);

        Assert.Null((await f.Db.ApprovalRequests.AsNoTracking().SingleAsync(a => a.Id == 901)).ApprovedAt);
    }

    [Fact]
    public async Task ApproveAction_SetsApprovedAtFromTheServerClock_InTheSameSaveAsTheStatus_AndDetailReturnsIt()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto());
        var before = DateTime.UtcNow.AddSeconds(-2);

        var approved = await f.Lifecycle.ApproveAsync(id, new ApprovalDecisionDto { Comment = "ok", EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav" });
        var detail = (await f.Queries.DetailAsync(id, default))!;
        var row = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync();

        Assert.Equal("Approved", approved.WorkflowStatus);
        Assert.NotNull(approved.ApprovedAt);
        Assert.InRange(approved.ApprovedAt!.Value, before, DateTime.UtcNow.AddSeconds(2));
        Assert.Equal(("Approved", "Siddhi Jadhav", approved.ApprovedAt), (row.WorkflowStatus, row.ApprovedBy, row.ApprovedAt));   // persisted together
        Assert.Equal(("Approved", "Siddhi Jadhav", row.ApprovedAt), (detail.WorkflowStatus, detail.ApprovedBy, detail.ApprovedAt));
        Assert.Null(detail.RejectedAt);
        Assert.Equal("Director Rao", detail.Approver);            // designated approver untouched
        Assert.Equal("APR-AB-0001", detail.ReferenceNo);
        Assert.Equal("Approved", Assert.Single(f.Db.ApprovalCycles.Where(c => c.ApprovalRequestId == id)).Status);
    }

    [Fact]
    public async Task ApproveAction_ClearsAStaleRejectedAt()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto());
        var row = await f.Db.ApprovalRequests.SingleAsync(); row.RejectedAt = DateTime.UtcNow.AddDays(-1); await f.Db.SaveChangesAsync();

        var approved = await f.Lifecycle.ApproveAsync(id, new ApprovalDecisionDto { EmployeeName = "A" });

        Assert.Null(approved.RejectedAt);
        Assert.NotNull(approved.ApprovedAt);
    }

    [Fact]
    public async Task RejectAction_SetsRejectedAt_AndNeverApprovedAt()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto());
        var before = DateTime.UtcNow.AddSeconds(-2);

        var rejected = await f.Lifecycle.RejectAsync(id, new ApprovalDecisionDto { Comment = "no budget", EmployeeName = "Richa Shah" });
        var detail = (await f.Queries.DetailAsync(id, default))!;

        Assert.Equal("Rejected", rejected.WorkflowStatus);
        Assert.InRange(rejected.RejectedAt!.Value, before, DateTime.UtcNow.AddSeconds(2));
        Assert.Null(rejected.ApprovedAt);
        Assert.Equal(("Richa Shah", rejected.RejectedAt, null), (detail.RejectedBy, detail.RejectedAt, detail.ApprovedAt));
    }

    [Fact]
    public async Task ChangesRequestedThenResubmitThenApprove_SetsApprovedAtOnlyOnTheFinalApproval()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto());

        var changes = await f.Lifecycle.RequestChangesAsync(id, new ApprovalDecisionDto { Comment = "revise" });
        Assert.Null(changes.ApprovedAt);
        var resubmitted = await f.Lifecycle.ResubmitAsync(id);
        Assert.Null(resubmitted.ApprovedAt);
        Assert.Equal(2, await f.Db.ApprovalCycles.CountAsync(c => c.ApprovalRequestId == id));

        var approved = await f.Lifecycle.ApproveAsync(id, new ApprovalDecisionDto { Comment = "ok", EmployeeName = "Siddhi Jadhav" });

        Assert.NotNull(approved.ApprovedAt);
        Assert.Equal("Approved", Assert.Single(f.Db.ApprovalCycles.Where(c => c.ApprovalRequestId == id && c.CycleNo == 2)).Status);
        Assert.Equal("ChangesRequested", Assert.Single(f.Db.ApprovalCycles.Where(c => c.ApprovalRequestId == id && c.CycleNo == 1)).Status);
    }

    [Fact]
    public async Task FailedApprove_LeavesApprovedAtNull()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto());
        await f.Lifecycle.RequestChangesAsync(id, new ApprovalDecisionDto { Comment = "revise" });

        await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => f.Lifecycle.ApproveAsync(id, new ApprovalDecisionDto { EmployeeName = "A" }));

        var row = await f.Db.ApprovalRequests.AsNoTracking().SingleAsync();
        Assert.Equal(("ChangesRequested", null, null), (row.WorkflowStatus, row.ApprovedAt, row.ApprovedBy));
    }

    [Fact]
    public async Task ExistingLifecycleRules_AreUnchanged()
    {
        await using var f = await Fx.CreateAsync();
        var id = await f.PostAsync(Dto());

        await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => f.Lifecycle.RejectAsync(id, new ApprovalDecisionDto { EmployeeName = "x" }));   // reason still required
        await f.Lifecycle.ApproveAsync(id, new ApprovalDecisionDto { Comment = "ok", EmployeeName = "A" });
        await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() => f.Lifecycle.ApproveAsync(id, new ApprovalDecisionDto { Comment = "again", EmployeeName = "B" }));

        Assert.Equal("A", (await f.Db.ApprovalRequests.AsNoTracking().SingleAsync()).ApprovedBy);   // failed repeat changed nothing
    }

    [Fact]
    public async Task DraftSave_AppliesApprovedByAndRejectedBy()
    {
        await using var f = await Fx.CreateAsync();
        f.Db.ApprovalRequests.Add(new ApprovalRequest { Id = 900, EaTaskId = 1, ReferenceNo = "APR-D", WorkflowStatus = "Draft", CreatedBy = "c", CreatedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();

        await f.Controller.SaveDraft(900, Dto(d => { d.ApprovedBy = "Draft A"; d.RejectedBy = null; }), default);

        Assert.Equal(("Draft A", null), ((await f.Db.ApprovalRequests.FindAsync(900L))!.ApprovedBy, (await f.Db.ApprovalRequests.FindAsync(900L))!.RejectedBy));
    }

    [Fact]
    public void NoUsersHrmsOrJwtDependency_InTheDecisionPath()
    {
        foreach (var p in typeof(ApprovalLifecycleService).GetConstructors().Single().GetParameters()
                     .Concat(typeof(ApprovalService).GetConstructors().Single().GetParameters()))
        {
            Assert.DoesNotContain("AppDbContext", p.ParameterType.Name);
            Assert.DoesNotContain("ActorResolver", p.ParameterType.Name);
        }
        Assert.DoesNotContain(typeof(ApprovalDecisionDto).GetProperties(), p => p.Name.Contains("UserId", StringComparison.Ordinal));
    }

    // ---------------- Swagger example (Approval + Travel create contracts) ----------------
    private static JsonObject ExampleFor(Type type)
    {
        var schema = new OpenApiSchema { Properties = new System.Collections.Generic.Dictionary<string, IOpenApiSchema>() };
        foreach (var p in type.GetProperties()) schema.Properties[JsonNamingPolicy.CamelCase.ConvertName(p.Name)] = new OpenApiSchema();
        new EaCreateRequestSchemaFilter().Apply(schema, new SchemaFilterContext(type, null!, null!));
        return Assert.IsType<JsonObject>(schema.Example);
    }

    [Fact]
    public void ApprovalPostSwaggerExample_ShowsApprovedByAndRejectedByAsNull_WithoutSampleData()
    {
        var example = ExampleFor(typeof(ApprovalRequestDto));

        Assert.True(example.ContainsKey("approvedBy") && example["approvedBy"] is null);
        Assert.True(example.ContainsKey("rejectedBy") && example["rejectedBy"] is null);
        Assert.True(example.ContainsKey("approverName") && example["approverName"] is null);
        Assert.All(example, kv => Assert.Null(kv.Value));
        Assert.Equal(typeof(ApprovalRequestDto).GetProperties().Length, example.Count);
    }

    [Fact]
    public void TravelPostSwaggerExample_ShowsApprovedByAndRejectedByAsNull_AndOneNeutralTravellerRow()
    {
        var example = ExampleFor(typeof(CreateTravelRequestDto));

        Assert.True(example.ContainsKey("approvedBy") && example["approvedBy"] is null);
        Assert.True(example.ContainsKey("rejectedBy") && example["rejectedBy"] is null);
        var traveller = Assert.IsType<JsonObject>(Assert.Single(Assert.IsType<JsonArray>(example["travellers"])));
        Assert.Equal(new[] { "contactInformation", "department", "employeePersonId", "travellerName" }, traveller.Select(kv => kv.Key).OrderBy(x => x));
        Assert.All(traveller, kv => Assert.Null(kv.Value));
        Assert.DoesNotContain("Sakshi", example.ToJsonString());
    }
}
