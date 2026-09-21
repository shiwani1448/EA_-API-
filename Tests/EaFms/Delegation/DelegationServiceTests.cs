using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// Step 2 CRUD/register tests. EaTaskService.CreateWithoutTatAsync runs Postgres-only raw
/// SQL and cannot execute against EF InMemory, so IEaTaskService is mocked exactly as in
/// TravelRequestServiceTests/TravelHistoryTests — the mock inserts a real EaTask row into
/// the same InMemory db, giving it a real identity Id. IAuditService is the real
/// AuditService so DELEGATION_CREATE/UPDATE rows are genuine, not hand-crafted.
/// </summary>
public class DelegationServiceTests
{
    private const string ModuleName = DelegationService.DelegationBusinessModuleName;

    private static EaFmsDbContext MakeDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static async Task<BusinessModule> AddModuleAsync(EaFmsDbContext db, string name, bool active = true)
    {
        var m = new BusinessModule { Name = name, IsActive = active, IsDeleted = false, CreatedBy = "tester", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(m);
        await db.SaveChangesAsync();
        return m;
    }

    private static async Task AddPriorityAsync(EaFmsDbContext db, string name, int level)
    {
        db.PriorityLevels.Add(new PriorityLevel { Name = name, Level = level, IsActive = true, IsDeleted = false, CreatedBy = "tester", CreatedDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private static (Mock<IDelegationNumberRepository> numbers, Mock<IEaTaskService> eaTasks) MakeCreateMocks(EaFmsDbContext db, long moduleId)
    {
        var numbers = new Mock<IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"DLG-2026-{Interlocked.Increment(ref seq):D6}");

        var eaTasks = new Mock<IEaTaskService>();
        eaTasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var task = new EaTask
                {
                    BusinessModuleId = moduleId, ModuleName = ModuleName, BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task, Description = dto.Description,
                    AllottedTatMinutes = null, WorkflowInstanceId = dto.WorkflowInstanceId,
                    ExecutionStatus = "NotStarted",
                    IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow
                };
                db.Tasks.Add(task);
                db.SaveChanges();
                return new EaTaskResponseDto
                {
                    EaTaskId = task.Id, ModuleId = moduleId, ModuleName = ModuleName,
                    BusinessRecordId = task.BusinessRecordId, Task = task.Task, Description = task.Description,
                    AllottedTatMinutes = null, ExecutionStatus = task.ExecutionStatus,
                    IsActive = true, CreatedBy = task.CreatedBy, CreatedDate = task.CreatedDate
                };
            });
        return (numbers, eaTasks);
    }

    private static (DelegationService service, Mock<IAuditService> auditSpy) MakeService(
        EaFmsDbContext db, Mock<IDelegationNumberRepository>? numbers = null, Mock<IEaTaskService>? eaTasks = null,
        string actor = "manager-1", long userId = 42)
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == actor && u.UserId == userId);
        var realAudit = new AuditService(db, user);
        var auditSpy = new Mock<IAuditService>();
        auditSpy.Setup(a => a.AddAudit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Callback<string, string, string, string, object?, object?, string?>(
                (a, m, e, id, old, next, d) => realAudit.AddAudit(a, m, e, id, old, next, d));

        numbers ??= new Mock<IDelegationNumberRepository>();
        eaTasks ??= new Mock<IEaTaskService>();
        return (new DelegationService(db, user, auditSpy.Object, numbers.Object, eaTasks.Object,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()), auditSpy);
    }

    private static DelegationCreateRequestDto MakeCreateDto(long sourceModuleId, string sourceEntityId = "51") => new()
    {
        Title = "Prepare board deck",
        Description = "Compile Q3 numbers",
        DoerId = "emp-42",
        DoerNameSnapshot = "Doer One",
        EndDate = DateTime.UtcNow.Date.AddDays(3),
        Priority = "High",
        SourceBusinessModuleId = sourceModuleId,
        SourceEntityId = sourceEntityId,
        SourceReference = "MTG-000051",
        AdditionalNotes = "Coordinate with finance"
    };

    // ----------------------------------------------------------------
    // CREATE
    // ----------------------------------------------------------------

    [Fact]
    public async Task Create_HappyPath_PersistsEverythingCorrectly()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        await AddPriorityAsync(db, "High", 3);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, auditSpy) = MakeService(db, numbers, eaTasks);

        var result = await service.CreateAsync(MakeCreateDto(source.Id));

        Assert.Matches(@"^DLG-\d{4}-\d{6}$", result.ReferenceNo);
        Assert.NotEqual(0, result.DelegationId);
        Assert.NotEqual(0, result.EaTaskId);
        Assert.Equal("Pending", result.Status);
        Assert.Equal("emp-42", result.DoerId);
        Assert.Equal("Doer One", result.DoerName);
        Assert.Equal("manager-1", result.AssignedById);
        Assert.Equal("High", result.Priority);
        Assert.Equal(source.Id, result.SourceBusinessModuleId);
        Assert.Equal("Meeting", result.SourceModuleName);
        Assert.Equal("51", result.SourceEntityId);
        Assert.Equal("MTG-000051", result.SourceReference);
        Assert.Equal("Coordinate with finance", result.AdditionalNotes);
        Assert.Null(result.StartedAt);
        Assert.Null(result.CompletedAt);

        // Exactly one EaTask, no TAT, no WorkflowInstance, no TatRule.
        var task = Assert.Single(await db.Tasks.ToListAsync());
        Assert.Equal(result.EaTaskId, task.Id);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Null(task.WorkflowInstanceId);
        Assert.Equal(result.DelegationId.ToString(), task.BusinessRecordId);
        Assert.Empty(await db.WorkflowInstances.ToListAsync());
        Assert.Empty(await db.TatRules.ToListAsync());

        auditSpy.Verify(a => a.AddAudit("DELEGATION_CREATE", "Delegation", nameof(Jarvis5.Entities.EaFms.Delegation),
            It.IsAny<string>(), null, It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.ActionType == "DELEGATION_CREATE");
    }

    [Fact]
    public async Task Create_WhenDelegationModuleMissing_ThrowsBeforeEaTaskCreation()
    {
        var db = MakeDb();
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, 999);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(MakeCreateDto(source.Id)));

        Assert.Contains("DELEGATION BUSINESS MODULE CONFIGURATION REQUIRED", ex.Message);
        Assert.Empty(await db.Delegations.ToListAsync());
        Assert.Empty(await db.Tasks.ToListAsync());
        eaTasks.Verify(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenSourceBusinessModuleInvalid_Throws()
    {
        var db = MakeDb();
        await AddModuleAsync(db, ModuleName);
        var (numbers, eaTasks) = MakeCreateMocks(db, 1);
        var (service, _) = MakeService(db, numbers, eaTasks);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(MakeCreateDto(sourceModuleId: 999)));
        Assert.Empty(await db.Delegations.ToListAsync());
    }

    // ----------------------------------------------------------------
    // EA-wide frontend-owned-requiredness cleanup: Priority is a frontend-owned
    // business string. PriorityLevel is optional discovery/reference data only —
    // it is no longer a persistence gate, so a Priority value with no matching
    // PriorityLevel row must NOT be rejected, and casing must NOT be rewritten to
    // match some canonical PriorityLevel.Name.
    // ----------------------------------------------------------------

    [Fact]
    public async Task Create_UnknownPriority_IsAcceptedAndStoredVerbatim()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var dto = MakeCreateDto(source.Id);
        dto.Priority = "Anything Selected By Frontend"; // not a configured PriorityLevel
        var result = await service.CreateAsync(dto);

        Assert.Equal("Anything Selected By Frontend", result.Priority);
    }

    [Fact]
    public async Task Create_PriorityCasingIsPreservedVerbatim_NotCanonicalized()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        await AddPriorityAsync(db, "High", 3);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var dto = MakeCreateDto(source.Id);
        dto.Priority = "high"; // lower-case input
        var result = await service.CreateAsync(dto);

        // Only whitespace is trimmed — casing is never rewritten against PriorityLevel.
        Assert.Equal("high", result.Priority);
    }

    [Fact]
    public async Task Create_PriorityWithSurroundingWhitespace_IsTrimmed()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var dto = MakeCreateDto(source.Id);
        dto.Priority = "  Urgent  ";
        var result = await service.CreateAsync(dto);

        Assert.Equal("Urgent", result.Priority);
    }

    [Fact]
    public async Task Create_NullPriority_IsAllowed()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var dto = MakeCreateDto(source.Id);
        dto.Priority = null;
        var result = await service.CreateAsync(dto);

        Assert.Null(result.Priority);
    }

    // ----------------------------------------------------------------
    // GET DETAIL
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetById_ReturnsPersistedDelegation_WithSourceModuleNameResolved()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Travel & Hospitality");
        await AddPriorityAsync(db, "High", 3);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);
        var created = await service.CreateAsync(MakeCreateDto(source.Id));

        var detail = await service.GetByIdAsync(created.DelegationId);

        Assert.Equal(created.ReferenceNo, detail.ReferenceNo);
        Assert.Equal("Travel & Hospitality", detail.SourceModuleName);
    }

    [Fact]
    public async Task GetById_UnknownId_ThrowsNotFound()
    {
        var db = MakeDb();
        var (service, _) = MakeService(db);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    [Fact]
    public async Task GetById_SoftDeleted_ThrowsNotFound()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        await AddPriorityAsync(db, "High", 3);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);
        var created = await service.CreateAsync(MakeCreateDto(source.Id));

        var entity = await db.Delegations.SingleAsync(d => d.Id == created.DelegationId);
        entity.IsDeleted = true;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(created.DelegationId));
    }

    // ----------------------------------------------------------------
    // UPDATE
    // ----------------------------------------------------------------

    [Fact]
    public async Task Update_EditableFields_PersistAndReferenceNoNeverChanges_NoSecondEaTask()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var otherSource = await AddModuleAsync(db, "EA Approval");
        await AddPriorityAsync(db, "High", 3);
        await AddPriorityAsync(db, "Low", 1);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, auditSpy) = MakeService(db, numbers, eaTasks);
        var created = await service.CreateAsync(MakeCreateDto(source.Id));

        var update = new DelegationUpdateRequestDto
        {
            Title = "Updated title", Description = "Updated description",
            DoerId = "emp-99", DoerNameSnapshot = "New Doer",
            EndDate = DateTime.UtcNow.Date.AddDays(10), Priority = "Low",
            SourceBusinessModuleId = otherSource.Id, SourceEntityId = "APR-2026-000010",
            SourceReference = "APR-000010", AdditionalNotes = "Revised notes"
        };

        var result = await service.UpdateAsync(created.DelegationId, update);

        Assert.Equal(created.ReferenceNo, result.ReferenceNo); // immutable
        Assert.Equal(created.EaTaskId, result.EaTaskId); // no second EaTask
        Assert.Equal("Updated title", result.Title);
        Assert.Equal("emp-99", result.DoerId);
        Assert.Equal("New Doer", result.DoerName);
        Assert.Equal("Low", result.Priority);
        Assert.Equal(otherSource.Id, result.SourceBusinessModuleId);
        Assert.Equal("EA Approval", result.SourceModuleName);
        Assert.Equal("APR-2026-000010", result.SourceEntityId);

        Assert.Single(await db.Tasks.ToListAsync()); // still exactly one EaTask
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.ActionType == "DELEGATION_UPDATE");
    }

    [Fact]
    public async Task Update_DoesNotAllowChangingAssignedByOrStatus_NoSuchFieldsExist()
    {
        // Structural guard: the update DTO has no AssignedBy/Status properties at all.
        var props = typeof(DelegationUpdateRequestDto).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.DoesNotContain("AssignedById", props);
        Assert.DoesNotContain("AssignedByNameSnapshot", props);
        Assert.DoesNotContain("Status", props);
        Assert.DoesNotContain("Id", props);
        Assert.DoesNotContain("ReferenceNo", props);
        Assert.DoesNotContain("EaTaskId", props);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Update_WhenCompleted_IsBlocked()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        await AddPriorityAsync(db, "High", 3);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);
        var created = await service.CreateAsync(MakeCreateDto(source.Id));

        var entity = await db.Delegations.SingleAsync(d => d.Id == created.DelegationId);
        entity.Status = "Completed";
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(created.DelegationId,
            new DelegationUpdateRequestDto { Title = "x", DoerId = "emp-1", SourceBusinessModuleId = source.Id, SourceEntityId = "1" }));
    }

    // ----------------------------------------------------------------
    // LIST / SEARCH / FILTER / VIEW
    // ----------------------------------------------------------------

    private static async Task<(EaFmsDbContext db, DelegationService service, BusinessModule source)> SeedRegisterAsync()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var otherSource = await AddModuleAsync(db, "Travel & Hospitality");
        await AddPriorityAsync(db, "High", 3);
        await AddPriorityAsync(db, "Low", 1);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        // Match the exact reference point DelegationService itself uses (IndiaBusinessCalendar.Today,
        // not DateTime.UtcNow.Date) so these assertions stay correct regardless of when the
        // test runs relative to the UTC/IST midnight boundary.
        var today = IndiaBusinessCalendar.Today;

        var a = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Prepare board deck", DoerId = "emp-1", DoerNameSnapshot = "Alice",
            EndDate = today, Priority = "High", SourceBusinessModuleId = source.Id,
            SourceEntityId = "51", SourceReference = "MTG-000051"
        });
        var b = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Chase travel booking", DoerId = "emp-2", DoerNameSnapshot = "Bob",
            EndDate = today.AddDays(-2), Priority = "Low", SourceBusinessModuleId = otherSource.Id,
            SourceEntityId = "9", SourceReference = "TRV-2026-000009"
        });
        var c = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Follow up notes", DoerId = "emp-1", DoerNameSnapshot = "Alice",
            EndDate = today.AddDays(5), SourceBusinessModuleId = source.Id, SourceEntityId = "52"
        });

        // Directly mutate persisted state to exercise InProgress/Completed without a
        // lifecycle service (Start/Complete arrive in a later step).
        var bEntity = await db.Delegations.SingleAsync(d => d.Id == b.DelegationId);
        bEntity.Status = "InProgress";
        var cEntity = await db.Delegations.SingleAsync(d => d.Id == c.DelegationId);
        cEntity.Status = "Completed";
        cEntity.DueDate = today.AddDays(-10); // would be "overdue" by date alone, but Completed excludes it
        await db.SaveChangesAsync();

        // A soft-deleted record that must never appear in any list result.
        var deleted = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Should never appear", DoerId = "emp-3", SourceBusinessModuleId = source.Id, SourceEntityId = "999"
        });
        var deletedEntity = await db.Delegations.SingleAsync(d => d.Id == deleted.DelegationId);
        deletedEntity.IsDeleted = true;
        await db.SaveChangesAsync();

        return (db, service, source);
    }

    [Fact]
    public async Task List_Pagination_IsDeterministic()
    {
        var (db, service, _) = await SeedRegisterAsync();
        var page1 = await service.ListAsync(new DelegationListQueryDto { PageNumber = 1, PageSize = 2 });
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalCount); // excludes the soft-deleted one
        var page2 = await service.ListAsync(new DelegationListQueryDto { PageNumber = 2, PageSize = 2 });
        Assert.Single(page2.Items);
        _ = db;
    }

    [Fact]
    public async Task Search_ByTitle_ReferenceDoerName_AndSourceReference_AllMatch()
    {
        var (_, service, _) = await SeedRegisterAsync();

        var byTitle = await service.ListAsync(new DelegationListQueryDto { Search = "board deck" });
        Assert.Single(byTitle.Items);

        var created = byTitle.Items[0];
        var byReference = await service.ListAsync(new DelegationListQueryDto { Search = created.ReferenceNo });
        Assert.Single(byReference.Items);

        var byDoerName = await service.ListAsync(new DelegationListQueryDto { Search = "bob" });
        Assert.Single(byDoerName.Items);
        Assert.Equal("emp-2", byDoerName.Items[0].DoerId);

        var bySourceReference = await service.ListAsync(new DelegationListQueryDto { Search = "TRV-2026-000009" });
        Assert.Single(bySourceReference.Items);
    }

    [Fact]
    public async Task Filter_Doer_Priority_Status_SourceModule_DueDate_AllNarrowCorrectly()
    {
        var (_, service, source) = await SeedRegisterAsync();

        var byDoer = await service.ListAsync(new DelegationListQueryDto { DoerId = "emp-1" });
        Assert.Equal(2, byDoer.Items.Count);

        var byPriority = await service.ListAsync(new DelegationListQueryDto { Priority = "high" });
        Assert.Single(byPriority.Items);

        var byStatus = await service.ListAsync(new DelegationListQueryDto { Status = "InProgress" });
        Assert.Single(byStatus.Items);

        var bySourceModule = await service.ListAsync(new DelegationListQueryDto { SourceBusinessModuleId = source.Id });
        Assert.Equal(2, bySourceModule.Items.Count); // "Prepare board deck" + "Follow up notes"

        var byDueDate = await service.ListAsync(new DelegationListQueryDto { EndDate = DateTime.UtcNow.Date });
        Assert.Single(byDueDate.Items);
    }

    [Theory]
    [InlineData("pending", 1)]
    [InlineData("inProgress", 1)]
    [InlineData("completed", 1)]
    public async Task View_PendingInProgressCompleted_MatchPersistedStatusExactly(string view, int expectedCount)
    {
        var (_, service, _) = await SeedRegisterAsync();
        var result = await service.ListAsync(new DelegationListQueryDto { View = view });
        Assert.Equal(expectedCount, result.Items.Count);
    }

    [Fact]
    public async Task View_DueToday_IsDerivedNotPersisted()
    {
        var (_, service, _) = await SeedRegisterAsync();
        var result = await service.ListAsync(new DelegationListQueryDto { View = "dueToday" });
        Assert.Single(result.Items);
        Assert.True(result.Items[0].IsDueToday);
    }

    [Fact]
    public async Task View_Overdue_IsDerived_AndExcludesCompletedEvenWithPastDueDate()
    {
        var (_, service, _) = await SeedRegisterAsync();
        var result = await service.ListAsync(new DelegationListQueryDto { View = "overdue" });

        Assert.Single(result.Items); // only "Chase travel booking" (InProgress, due -2 days)
        Assert.True(result.Items[0].IsOverdue);
        Assert.DoesNotContain(result.Items, x => x.Status == "Completed"); // Completed never counts as overdue
    }

    [Fact]
    public async Task ConflictingViewAndStatus_IsRejectedDeterministically()
    {
        var (_, service, _) = await SeedRegisterAsync();
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.ListAsync(new DelegationListQueryDto { View = "pending", Status = "Completed" }));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.ListAsync(new DelegationListQueryDto { View = "overdue", Status = "Completed" }));
    }

    [Fact]
    public async Task List_NeverReturnsSoftDeletedRecords()
    {
        var (_, service, _) = await SeedRegisterAsync();
        var all = await service.ListAsync(new DelegationListQueryDto { View = "all", PageSize = 100 });
        Assert.DoesNotContain(all.Items, x => x.Title == "Should never appear");
    }

    // ----------------------------------------------------------------
    // FILTER COMPOSITION
    // ----------------------------------------------------------------

    [Fact]
    public async Task Filters_SearchAndDoer_ComposeTogether()
    {
        var (_, service, _) = await SeedRegisterAsync();
        // "Alice" (emp-1) owns "Prepare board deck" and "Follow up notes"; searching "board"
        // must narrow to just the one that also matches the search term.
        var result = await service.ListAsync(new DelegationListQueryDto { DoerId = "emp-1", Search = "board" });
        Assert.Single(result.Items);
        Assert.Equal("emp-1", result.Items[0].DoerId);
    }

    [Fact]
    public async Task Filters_PriorityAndSourceModule_ComposeTogether()
    {
        var (_, service, source) = await SeedRegisterAsync();
        var result = await service.ListAsync(new DelegationListQueryDto { Priority = "High", SourceBusinessModuleId = source.Id });
        Assert.Single(result.Items);
        Assert.Equal("High", result.Items[0].Priority);
        Assert.Equal(source.Id, result.Items[0].SourceBusinessModuleId);
    }

    [Fact]
    public async Task Filters_ViewOverdueAndPriority_ComposeTogether()
    {
        var (_, service, _) = await SeedRegisterAsync();
        var matching = await service.ListAsync(new DelegationListQueryDto { View = "overdue", Priority = "Low" });
        Assert.Single(matching.Items); // "Chase travel booking": InProgress, -2 days, Low

        var nonMatching = await service.ListAsync(new DelegationListQueryDto { View = "overdue", Priority = "High" });
        Assert.Empty(nonMatching.Items);
    }

    [Fact]
    public async Task Filters_ViewInProgressAndDoer_ComposeTogether()
    {
        var (_, service, _) = await SeedRegisterAsync();
        var matching = await service.ListAsync(new DelegationListQueryDto { View = "inProgress", DoerId = "emp-2" });
        Assert.Single(matching.Items);

        var nonMatching = await service.ListAsync(new DelegationListQueryDto { View = "inProgress", DoerId = "emp-1" });
        Assert.Empty(nonMatching.Items);
    }

    // ----------------------------------------------------------------
    // KPI SUMMARY
    // ----------------------------------------------------------------

    [Fact]
    public async Task Summary_CountsMatchExactly_AndExcludesSoftDeleted()
    {
        var (_, service, _) = await SeedRegisterAsync();
        var summary = await service.GetSummaryAsync();

        // Seed: a=Pending(due today), b=InProgress(due -2), c=Completed(due -10, would be
        // overdue by date alone), plus one soft-deleted record that must never be counted.
        Assert.Equal(3, summary.Total);
        Assert.Equal(1, summary.Pending);
        Assert.Equal(1, summary.InProgress);
        Assert.Equal(1, summary.Completed);
        Assert.Equal(1, summary.DueToday); // "a" only
        Assert.Equal(1, summary.Overdue);  // "b" only — "c" is Completed, excluded despite due -10
    }

    [Fact]
    public async Task Summary_CompletedPastDueItem_DoesNotCountAsOverdue()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var created = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Old completed item", DoerId = "emp-1",
            EndDate = IndiaBusinessCalendar.Today.AddDays(-30),
            SourceBusinessModuleId = source.Id, SourceEntityId = "1"
        });
        var entity = await db.Delegations.SingleAsync(d => d.Id == created.DelegationId);
        entity.Status = "Completed";
        await db.SaveChangesAsync();

        var summary = await service.GetSummaryAsync();
        Assert.Equal(0, summary.Overdue);
        Assert.Equal(1, summary.Completed);
    }

    [Fact]
    public async Task Summary_CompletedDueTodayItem_DoesNotCountAsDueToday()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var created = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Completed today", DoerId = "emp-1",
            EndDate = IndiaBusinessCalendar.Today,
            SourceBusinessModuleId = source.Id, SourceEntityId = "1"
        });
        var entity = await db.Delegations.SingleAsync(d => d.Id == created.DelegationId);
        entity.Status = "Completed";
        await db.SaveChangesAsync();

        var summary = await service.GetSummaryAsync();
        Assert.Equal(0, summary.DueToday);
        Assert.Equal(1, summary.Completed);
    }

    [Fact]
    public async Task Summary_NoDelegations_ReturnsAllZeros()
    {
        var db = MakeDb();
        var (service, _) = MakeService(db);
        var summary = await service.GetSummaryAsync();

        Assert.Equal(0, summary.Total);
        Assert.Equal(0, summary.Pending);
        Assert.Equal(0, summary.InProgress);
        Assert.Equal(0, summary.Completed);
        Assert.Equal(0, summary.DueToday);
        Assert.Equal(0, summary.Overdue);
    }

    // ----------------------------------------------------------------
    // LIFECYCLE — START (Step 4)
    // ----------------------------------------------------------------

    private static async Task<(EaFmsDbContext db, DelegationService service, DelegationResponseDto created, Mock<IAuditService> auditSpy)>
        SeedPendingDelegationAsync()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        await AddPriorityAsync(db, "High", 3);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, auditSpy) = MakeService(db, numbers, eaTasks);
        var created = await service.CreateAsync(MakeCreateDto(source.Id));
        return (db, service, created, auditSpy);
    }

    [Fact]
    public async Task Start_FromPending_Succeeds_AndSynchronizesEaTaskAtomically()
    {
        var (db, service, created, auditSpy) = await SeedPendingDelegationAsync();

        var result = await service.StartAsync(created.DelegationId);

        Assert.Equal("InProgress", result.Status);
        Assert.NotNull(result.StartedAt);
        Assert.Null(result.CompletedAt);

        var task = await db.Tasks.SingleAsync(t => t.Id == created.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.InProgress, task.ExecutionStatus);
        Assert.NotNull(task.StartedAt);
        Assert.Equal(result.StartedAt, task.StartedAt); // same authoritative `now`, not two separate timestamps
        Assert.Null(task.CompletedAt);

        // Delegation has no TAT and no WorkflowInstance — Start must not fabricate either.
        Assert.Null(task.TatRuleId);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Null(task.TatUsedMinutes);
        Assert.Null(task.WorkflowInstanceId);

        auditSpy.Verify(a => a.AddAudit("DELEGATION_START", "Delegation", nameof(Jarvis5.Entities.EaFms.Delegation),
            created.DelegationId.ToString(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
        Assert.Contains(await db.AuditLogs.ToListAsync(), a => a.ActionType == "DELEGATION_START");
    }

    [Fact]
    public async Task Start_DoesNotChangeAssignmentOrSource()
    {
        var (db, service, created, _) = await SeedPendingDelegationAsync();
        await service.StartAsync(created.DelegationId);

        var reloaded = await service.GetByIdAsync(created.DelegationId);
        Assert.Equal(created.DoerId, reloaded.DoerId);
        Assert.Equal(created.DoerName, reloaded.DoerName);
        Assert.Equal(created.AssignedById, reloaded.AssignedById);
        Assert.Equal(created.SourceBusinessModuleId, reloaded.SourceBusinessModuleId);
        Assert.Equal(created.SourceEntityId, reloaded.SourceEntityId);
        Assert.Equal(created.SourceReference, reloaded.SourceReference);
        _ = db;
    }

    [Fact]
    public async Task Start_WhenAlreadyInProgress_ThrowsConflict_AndDoesNotResetStartedAt()
    {
        var (db, service, created, _) = await SeedPendingDelegationAsync();
        var firstStart = await service.StartAsync(created.DelegationId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.StartAsync(created.DelegationId));
        Assert.Contains("cannot be started", ex.Message);

        var reloaded = await service.GetByIdAsync(created.DelegationId);
        Assert.Equal(firstStart.StartedAt, reloaded.StartedAt);
        Assert.Equal(1, (await db.AuditLogs.ToListAsync()).Count(a => a.ActionType == "DELEGATION_START"));
    }

    [Fact]
    public async Task Start_WhenAlreadyCompleted_ThrowsConflict()
    {
        var (db, service, created, _) = await SeedPendingDelegationAsync();
        await service.StartAsync(created.DelegationId);
        await service.CompleteAsync(created.DelegationId, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.StartAsync(created.DelegationId));
        _ = db;
    }

    [Fact]
    public async Task Start_UnknownId_ThrowsNotFound()
    {
        var db = MakeDb();
        var (service, _) = MakeService(db);
        await Assert.ThrowsAsync<NotFoundException>(() => service.StartAsync(999));
    }

    // ----------------------------------------------------------------
    // LIFECYCLE — COMPLETE (Step 4)
    // ----------------------------------------------------------------

    [Fact]
    public async Task Complete_FromInProgress_Succeeds_AndSynchronizesEaTaskAtomically()
    {
        var (db, service, created, auditSpy) = await SeedPendingDelegationAsync();
        var started = await service.StartAsync(created.DelegationId);

        var result = await service.CompleteAsync(created.DelegationId, null);

        Assert.Equal("Completed", result.Status);
        Assert.Equal(started.StartedAt, result.StartedAt); // original start preserved
        Assert.NotNull(result.CompletedAt);
        Assert.Equal("manager-1", result.CompletedById); // Actor(), same convention as AssignedById
        Assert.NotNull(result.CompletedByName);

        var task = await db.Tasks.SingleAsync(t => t.Id == created.EaTaskId);
        Assert.Equal(EaTaskExecutionStatus.Completed, task.ExecutionStatus);
        Assert.Equal(started.StartedAt, task.StartedAt);
        Assert.Equal(result.CompletedAt, task.CompletedAt); // same authoritative `now`
        Assert.Null(task.TatRuleId);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Null(task.TatUsedMinutes);
        Assert.Null(task.WorkflowInstanceId);

        auditSpy.Verify(a => a.AddAudit("DELEGATION_COMPLETE", "Delegation", nameof(Jarvis5.Entities.EaFms.Delegation),
            created.DelegationId.ToString(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Complete_WhilePending_ThrowsConflict()
    {
        var (db, service, created, _) = await SeedPendingDelegationAsync();
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(created.DelegationId, null));
        Assert.Contains("cannot be completed", ex.Message);
        _ = db;
    }

    [Fact]
    public async Task Complete_WhenAlreadyCompleted_ThrowsConflict_AndDoesNotCreateSecondCompletionEvent()
    {
        var (db, service, created, _) = await SeedPendingDelegationAsync();
        await service.StartAsync(created.DelegationId);
        await service.CompleteAsync(created.DelegationId, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CompleteAsync(created.DelegationId, null));
        Assert.Equal(1, (await db.AuditLogs.ToListAsync()).Count(a => a.ActionType == "DELEGATION_COMPLETE"));
    }

    [Fact]
    public async Task Complete_UnknownId_ThrowsNotFound()
    {
        var db = MakeDb();
        var (service, _) = MakeService(db);
        await Assert.ThrowsAsync<NotFoundException>(() => service.CompleteAsync(999, null));
    }

    // ----------------------------------------------------------------
    // LIFECYCLE EFFECT ON KPI / DERIVED VIEWS (Step 4)
    // ----------------------------------------------------------------

    [Fact]
    public async Task Lifecycle_MovesItemBetweenPendingInProgressCompletedViews_AndSummaryCounts()
    {
        var (db, service, created, _) = await SeedPendingDelegationAsync();

        var beforeStart = await service.GetSummaryAsync();
        Assert.Equal(1, beforeStart.Pending);
        Assert.Equal(0, beforeStart.InProgress);
        Assert.Single((await service.ListAsync(new DelegationListQueryDto { View = "pending" })).Items);
        Assert.Empty((await service.ListAsync(new DelegationListQueryDto { View = "inProgress" })).Items);

        await service.StartAsync(created.DelegationId);

        var afterStart = await service.GetSummaryAsync();
        Assert.Equal(0, afterStart.Pending);
        Assert.Equal(1, afterStart.InProgress);
        Assert.Empty((await service.ListAsync(new DelegationListQueryDto { View = "pending" })).Items);
        Assert.Single((await service.ListAsync(new DelegationListQueryDto { View = "inProgress" })).Items);

        await service.CompleteAsync(created.DelegationId, null);

        var afterComplete = await service.GetSummaryAsync();
        Assert.Equal(0, afterComplete.InProgress);
        Assert.Equal(1, afterComplete.Completed);
        Assert.Empty((await service.ListAsync(new DelegationListQueryDto { View = "inProgress" })).Items);
        Assert.Single((await service.ListAsync(new DelegationListQueryDto { View = "completed" })).Items);
        _ = db;
    }

    [Fact]
    public async Task Complete_PastDueItem_LeavesOverdueView_AndIsOverdueBecomesFalse()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);
        var created = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Overdue then completed", DoerId = "emp-1",
            EndDate = IndiaBusinessCalendar.Today.AddDays(-5),
            SourceBusinessModuleId = source.Id, SourceEntityId = "1"
        });
        await service.StartAsync(created.DelegationId);

        var beforeComplete = await service.GetByIdAsync(created.DelegationId);
        Assert.True(beforeComplete.IsOverdue);
        Assert.Single((await service.ListAsync(new DelegationListQueryDto { View = "overdue" })).Items);

        var completed = await service.CompleteAsync(created.DelegationId, null);

        Assert.False(completed.IsOverdue);
        Assert.Empty((await service.ListAsync(new DelegationListQueryDto { View = "overdue" })).Items);
        Assert.Equal(0, (await service.GetSummaryAsync()).Overdue);
    }

    [Fact]
    public async Task Complete_DueTodayItem_LeavesDueTodayView_AndIsDueTodayBecomesFalse()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);
        var created = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Due today then completed", DoerId = "emp-1",
            EndDate = IndiaBusinessCalendar.Today,
            SourceBusinessModuleId = source.Id, SourceEntityId = "1"
        });
        await service.StartAsync(created.DelegationId);

        Assert.Single((await service.ListAsync(new DelegationListQueryDto { View = "dueToday" })).Items);

        var completed = await service.CompleteAsync(created.DelegationId, null);

        Assert.False(completed.IsDueToday);
        Assert.Empty((await service.ListAsync(new DelegationListQueryDto { View = "dueToday" })).Items);
        Assert.Equal(0, (await service.GetSummaryAsync()).DueToday);
    }

    // ----------------------------------------------------------------
    // MANUAL / DIRECT DELEGATION SOURCE CLEANUP
    //
    // Architectural correction: there is no "Manual / Direct Delegation" BusinessModule.
    // A direct/manual Delegation has SourceBusinessModuleId/SourceEntityId/SourceReference
    // all null — never a fake source module, never defaulted to the Delegation module
    // itself (that would mean "Delegation originated from Delegation").
    // ----------------------------------------------------------------

    [Fact]
    public async Task Create_ManualDelegation_AllSourceFieldsNull_NoSourceModuleRequired()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        await AddPriorityAsync(db, "High", 3);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, auditSpy) = MakeService(db, numbers, eaTasks);

        // Exactly what a manual/direct Delegation frontend submission looks like: no source
        // fields at all — no fake "Manual / Direct Delegation" module to pick from a dropdown.
        var result = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Prepare quarterly summary",
            Description = "Ad hoc EA request, no source record",
            DoerId = "emp-manual-1",
            DoerNameSnapshot = "Manual Doer",
            Priority = "High"
        });

        Assert.Equal("Pending", result.Status);
        Assert.Equal("emp-manual-1", result.DoerId);
        Assert.Equal("Manual Doer", result.DoerName);
        Assert.Equal("manager-1", result.AssignedById); // server-owned actor, unchanged
        Assert.Null(result.SourceBusinessModuleId);
        Assert.Null(result.SourceModuleName);
        Assert.Null(result.SourceEntityId); // never fabricated as "" or defaulted
        Assert.Null(result.SourceReference);
        Assert.Equal("High", result.Priority);
        Assert.Null(result.StartedAt);
        Assert.Null(result.CompletedAt);

        var task = await db.Tasks.SingleAsync(t => t.Id == result.EaTaskId);
        Assert.Equal("NotStarted", task.ExecutionStatus);
        Assert.Null(task.StartedAt);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.TatRuleId);
        Assert.Null(task.AllottedTatMinutes);
        Assert.Null(task.TatUsedMinutes);
        Assert.Null(task.WorkflowInstanceId);

        auditSpy.Verify(a => a.AddAudit("DELEGATION_CREATE", "Delegation", nameof(Jarvis5.Entities.EaFms.Delegation),
            It.IsAny<string>(), null, It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Create_ManualDelegation_ExplicitNullSourceFields_SameAsOmitted()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var result = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Prepare quarterly summary",
            DoerId = "emp-manual-1",
            SourceBusinessModuleId = null,
            SourceEntityId = null,
            SourceReference = null
        });

        Assert.Null(result.SourceBusinessModuleId);
        Assert.Null(result.SourceModuleName);
        Assert.Null(result.SourceEntityId);
        Assert.Null(result.SourceReference);
    }

    [Fact]
    public async Task Create_ManualDelegation_EaTask_StillBelongsToCanonicalDelegationModule()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var result = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Send report",
            DoerId = "emp-manual-3"
        });

        var task = await db.Tasks.SingleAsync(t => t.Id == result.EaTaskId);
        Assert.Equal(module.Id, task.BusinessModuleId); // canonical "Delegation" module, unaffected by source
    }

    [Fact]
    public async Task Create_ManualDelegation_WithOptionalSourceReferenceOnly_Persists()
    {
        // SourceReference is a free-text business note independent of SourceBusinessModuleId/
        // SourceEntityId — a manual Delegation may still carry one without implying a real source.
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var result = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Follow up with vendor",
            DoerId = "emp-manual-2",
            SourceReference = "Verbal instruction from EA on 2026-09-18"
        });

        Assert.Equal("Verbal instruction from EA on 2026-09-18", result.SourceReference);
        Assert.Null(result.SourceBusinessModuleId);
        Assert.Null(result.SourceEntityId);
    }

    [Fact]
    public async Task Create_RealSourceModule_StillRequiresValidBusinessModule_AndStillWorks()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var result = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Chase meeting action",
            DoerId = "emp-1",
            SourceBusinessModuleId = source.Id,
            SourceEntityId = "MTG-ACTION-4",
            SourceReference = "MTG-000060"
        });

        Assert.Equal(source.Id, result.SourceBusinessModuleId);
        Assert.Equal("Meeting", result.SourceModuleName);
        Assert.Equal("MTG-ACTION-4", result.SourceEntityId);
    }

    [Fact]
    public async Task Create_NonNullInvalidSourceModule_StillRejected()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Should fail",
            DoerId = "emp-1",
            SourceBusinessModuleId = 999999
        }));
        Assert.Empty(await db.Delegations.ToListAsync());
    }

    [Fact]
    public async Task List_FilterBySourceBusinessModuleId_StillWorks_ManualDelegationsExcluded()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var sourced = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Sourced", DoerId = "emp-1", SourceBusinessModuleId = source.Id, SourceEntityId = "1"
        });
        var manual = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Manual", DoerId = "emp-2"
        });

        var filtered = await service.ListAsync(new DelegationListQueryDto { SourceBusinessModuleId = source.Id });

        Assert.Single(filtered.Items);
        Assert.Equal(sourced.DelegationId, filtered.Items[0].DelegationId);
        Assert.DoesNotContain(filtered.Items, i => i.DelegationId == manual.DelegationId);
    }

    [Fact]
    public async Task Update_CanClearSourceFieldsBackToNull()
    {
        var db = MakeDb();
        var module = await AddModuleAsync(db, ModuleName);
        var source = await AddModuleAsync(db, "Meeting");
        var (numbers, eaTasks) = MakeCreateMocks(db, module.Id);
        var (service, _) = MakeService(db, numbers, eaTasks);

        var created = await service.CreateAsync(new DelegationCreateRequestDto
        {
            Title = "Sourced initially", DoerId = "emp-1",
            SourceBusinessModuleId = source.Id, SourceEntityId = "1", SourceReference = "MTG-1"
        });

        var updated = await service.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto
        {
            Title = "Now manual", DoerId = "emp-1",
            SourceBusinessModuleId = null, SourceEntityId = null, SourceReference = null
        });

        Assert.Null(updated.SourceBusinessModuleId);
        Assert.Null(updated.SourceModuleName);
        Assert.Null(updated.SourceEntityId);
        Assert.Null(updated.SourceReference);
    }
}
