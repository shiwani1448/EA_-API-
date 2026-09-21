using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// ApprovedBy / RejectedBy on Travel & Hospitality: business data (who actually approved / rejected),
/// separate from the designated approver (ApproverId / ApproverNameSnapshot) and from audit attribution.
/// </summary>
public class TravelApprovedRejectedByTests
{
    private sealed class Fx : IAsyncDisposable
    {
        public EaFmsDbContext Db { get; } = new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        public TravelRequestService Service { get; }
        public Mock<IEaActorResolver> Resolver { get; } = new(MockBehavior.Strict);

        public Fx()
        {
            Resolver.Setup(r => r.ResolveDisplayNameAsync(It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("EA User");
            var module = new BusinessModule { Name = TravelRequestService.TravelBusinessModuleName, IsActive = true, CreatedBy = "t", CreatedDate = DateTime.UtcNow };
            Db.BusinessModules.Add(module);
            Db.SaveChanges();
            var numbers = new Mock<ITravelNumberRepository>();
            var n = 0;
            numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"TRV-AB-{++n:D6}");
            var tasks = new Mock<IEaTaskService>();
            tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CreateEaTaskDto d, CancellationToken _) =>
                {
                    var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = d.BusinessRecordId, Task = d.Task,
                        ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "t", CreatedDate = DateTime.UtcNow };
                    Db.Tasks.Add(t); Db.SaveChanges();
                    return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = t.BusinessRecordId,
                        Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = "t", CreatedDate = t.CreatedDate };
                });
            var user = Mock.Of<ICurrentUserService>(u => u.UserId == 0 && u.UserName == "reviewer");
            Service = new TravelRequestService(Db, Mock.Of<IAuditService>(), user, numbers.Object, tasks.Object, Resolver.Object);
        }

        public Task<TravelRequestCreatedDto> CreateAsync(Action<CreateTravelRequestDto>? tweak = null, bool approval = true)
        {
            var d = new CreateTravelRequestDto { UserId = 1, ApprovalRequired = approval, ApproverId = "manager-1" };
            tweak?.Invoke(d);
            return Service.CreateDraftAsync(d);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task Post_AcceptsApprovedByAndRejectedBy_PersistsThem_AndReturnsThemOnPostDetailAndList()
    {
        await using var f = new Fx();

        var created = await f.CreateAsync(d => { d.ApprovedBy = "  Imported Approver "; d.RejectedBy = "Imported Rejecter"; });
        var detail = await f.Service.GetByIdAsync(created.TravelRequestId);
        var listed = Assert.Single((await f.Service.ListAsync(new TravelRequestListQueryDto())).Items);
        var row = await f.Db.TravelRequests.AsNoTracking().SingleAsync();

        Assert.Equal(("Imported Approver", "Imported Rejecter"), (row.ApprovedBy, row.RejectedBy));
        Assert.Equal(("Imported Approver", "Imported Rejecter"), (created.ApprovedBy, created.RejectedBy));
        Assert.Equal(("Imported Approver", "Imported Rejecter"), (detail.Approval.ApprovedBy, detail.Approval.RejectedBy));
        Assert.Equal(("Imported Approver", "Imported Rejecter"), (listed.ApprovedBy, listed.RejectedBy));
    }

    [Fact]
    public async Task Post_WithBothNullOrBlank_StoresNull_AndExistingCreateStillWorks()
    {
        await using var f = new Fx();

        var a = await f.CreateAsync();
        var b = await f.CreateAsync(d => { d.ApprovedBy = "  "; d.RejectedBy = ""; });

        foreach (var id in new[] { a.TravelRequestId, b.TravelRequestId })
        {
            var detail = await f.Service.GetByIdAsync(id);
            Assert.Null(detail.Approval.ApprovedBy);
            Assert.Null(detail.Approval.RejectedBy);
            Assert.Equal("Draft", detail.BusinessState);
        }
        Assert.Equal("Draft", a.BusinessState);
        Assert.Equal("TRV-AB-000001", a.ReferenceNo);
    }

    [Fact]
    public async Task ApproverIsDesignated_ApprovedByIsDecisionActor_AreSeparate()
    {
        await using var f = new Fx();

        var created = await f.CreateAsync(d => d.ApprovedBy = "Someone Else");
        var detail = await f.Service.GetByIdAsync(created.TravelRequestId);

        Assert.Equal(("manager-1", "Someone Else"), (detail.Approval.ApproverId, detail.Approval.ApprovedBy));
    }

    [Fact]
    public async Task DraftUpdate_AppliesApprovedByAndRejectedBy_AndCanClearThem()
    {
        await using var f = new Fx();
        var created = await f.CreateAsync(d => d.ApprovedBy = "first");

        var updated = await f.Service.UpdateDraftAsync(created.TravelRequestId, new UpdateTravelDraftDto { ApprovalRequired = true, ApproverId = "manager-1", ApprovedBy = "second", RejectedBy = "r" });
        Assert.Equal(("second", "r"), (updated.Approval.ApprovedBy, updated.Approval.RejectedBy));

        var cleared = await f.Service.UpdateDraftAsync(created.TravelRequestId, new UpdateTravelDraftDto { ApprovalRequired = true, ApproverId = "manager-1" });
        Assert.Equal((null, null), (cleared.Approval.ApprovedBy, cleared.Approval.RejectedBy));
    }

    [Fact]
    public async Task ApproveAction_SetsApprovedByToTheActor_ClearsRejectedBy_WithoutChangingTheWorkflow()
    {
        await using var f = new Fx();
        var created = await f.CreateAsync(d => d.RejectedBy = "stale");
        await f.Service.SubmitAsync(created.TravelRequestId);

        var result = await f.Service.ApproveAsync(created.TravelRequestId,
            new ApproveTravelRequestDto { ExpectedCycleNo = 1, DecisionComment = "ok", EmployeeId = "S5I-1013", EmployeeName = "Siddhi Jadhav" });
        var detail = await f.Service.GetByIdAsync(created.TravelRequestId);

        Assert.Equal(("Approved", "Upcoming"), (result.ApprovalState, result.BusinessState));   // existing workflow
        Assert.NotNull(result.ApprovedAt);
        Assert.Equal(("Siddhi Jadhav", null), (result.ApprovedBy, result.RejectedBy));
        Assert.Equal(("Siddhi Jadhav", null, "manager-1"), (detail.Approval.ApprovedBy, detail.Approval.RejectedBy, detail.Approval.ApproverId));
    }

    [Fact]
    public async Task RejectAction_SetsRejectedByToTheActor_ClearsApprovedBy()
    {
        await using var f = new Fx();
        var created = await f.CreateAsync(d => d.ApprovedBy = "stale");
        await f.Service.SubmitAsync(created.TravelRequestId);

        var result = await f.Service.RejectAsync(created.TravelRequestId,
            new RejectTravelRequestDto { ExpectedCycleNo = 1, DecisionComment = "no", EmployeeName = "Richa Shah" });

        Assert.Equal(("Rejected", "Draft"), (result.ApprovalState, result.BusinessState));
        Assert.NotNull(result.RejectedAt);
        Assert.Equal(("Richa Shah", null), (result.RejectedBy, result.ApprovedBy));
    }

    [Fact]
    public async Task Decision_WithoutActor_NeverBorrowsTheDesignatedApprover_AndKeepsTheLifecycle()
    {
        await using var f = new Fx();
        var created = await f.CreateAsync();
        await f.Service.SubmitAsync(created.TravelRequestId);

        var result = await f.Service.ApproveAsync(created.TravelRequestId, new ApproveTravelRequestDto { ExpectedCycleNo = 1 });

        Assert.Equal("Approved", result.ApprovalState);
        Assert.Null(result.ApprovedBy);
        await Assert.ThrowsAsync<Jarvis5.Common.BusinessRuleException>(() =>
            f.Service.ApproveAsync(created.TravelRequestId, new ApproveTravelRequestDto { ExpectedCycleNo = 1, EmployeeName = "again" }));
        Assert.Null((await f.Db.TravelRequests.AsNoTracking().SingleAsync()).ApprovedBy);   // failed repeat changed nothing
    }

    [Fact]
    public async Task DecisionActions_DoNotCallTheUsersResolver_AndHistoricalNullRowsStillWork()
    {
        await using var f = new Fx();
        f.Db.TravelRequests.Add(new TravelRequest { Id = 500, EaTaskId = 9, ReferenceNo = "OLD", BusinessState = "Draft", ApprovalRequired = true,
            ApprovalState = "NotSubmitted", ApproverId = "m", CreatedBy = "old", CreatedDate = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        f.Resolver.Invocations.Clear();

        await f.Service.SubmitAsync(500);
        var approved = await f.Service.ApproveAsync(500, new ApproveTravelRequestDto { ExpectedCycleNo = 1, EmployeeName = "A" });
        var detail = await f.Service.GetByIdAsync(500);

        f.Resolver.Verify(r => r.ResolveDisplayNameAsync(It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Resolver.Verify(r => r.FindUserContactAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("A", approved.ApprovedBy);
        Assert.Equal(("A", null), (detail.Approval.ApprovedBy, detail.Approval.RejectedBy));
    }

    [Fact]
    public void Validators_BoundLengthOnly_NoMandatoryDecisionFields()
    {
        var create = new CreateTravelRequestDtoValidator();
        var update = new UpdateTravelDraftDtoValidator();

        Assert.True(create.Validate(new CreateTravelRequestDto()).IsValid);
        Assert.True(update.Validate(new UpdateTravelDraftDto()).IsValid);
        Assert.False(create.Validate(new CreateTravelRequestDto { ApprovedBy = new string('x', 201) }).IsValid);
        Assert.False(update.Validate(new UpdateTravelDraftDto { RejectedBy = new string('x', 201) }).IsValid);
        Assert.True(create.Validate(new CreateTravelRequestDto { ApprovedBy = new string('x', 200) }).IsValid);
    }
}
