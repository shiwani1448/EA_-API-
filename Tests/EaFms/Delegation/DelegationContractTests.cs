using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// Delegation public contract: delegationType + startDate (new columns), endDate (API name over the
/// DueDate column) and doerId/doerNameSnapshot/doerName (API names match the DoerId/DoerNameSnapshot columns).
/// </summary>
public class DelegationContractTests
{
    private static readonly DateTime Start = new(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

    private sealed class Fx
    {
        public required EaFmsDbContext Db { get; init; }
        public required DelegationService Svc { get; init; }
    }

    private static async Task<Fx> NewAsync()
    {
        var env = Mock.Of<IWebHostEnvironment>(e => e.ContentRootPath == Path.GetTempPath());
        var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);
        var module = new BusinessModule { Name = DelegationService.DelegationBusinessModuleName, IsActive = true, CreatedBy = "seed", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 42L);
        var numbers = new Mock<IDelegationNumberRepository>();
        var seq = 0;
        numbers.Setup(r => r.GenerateNextReferenceNoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => $"DLG-C-{++seq:D6}");
        var tasks = new Mock<IEaTaskService>();
        tasks.Setup(s => s.CreateWithoutTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = dto.BusinessRecordId,
                    Task = dto.Task ?? dto.BusinessRecordId, ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "ea-actor", CreatedDate = DateTime.UtcNow };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
            });
        // Type-only TAT path (delegationType supplied): same fake task, with the type snapshot and a TAT rule stand-in.
        tasks.Setup(s => s.CreateWithTypeOnlyTatAsync(It.IsAny<CreateEaTaskDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateEaTaskDto dto, CancellationToken _) =>
            {
                var t = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = dto.BusinessRecordId, Type = dto.Type, Subtype = null,
                    Task = dto.Task ?? dto.BusinessRecordId, ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "ea-actor", CreatedDate = DateTime.UtcNow };
                db.Tasks.Add(t); db.SaveChanges();
                return new EaTaskResponseDto { EaTaskId = t.Id, ModuleId = module.Id, ModuleName = t.ModuleName, BusinessRecordId = t.BusinessRecordId,
                    Task = t.Task, ExecutionStatus = t.ExecutionStatus, IsActive = true, CreatedBy = t.CreatedBy, CreatedDate = t.CreatedDate };
            });
        return new Fx { Db = db, Svc = new DelegationService(db, user, new Mock<IAuditService>().Object, numbers.Object, tasks.Object, env) };
    }

    private static DelegationCreateRequestDto Request(string? type = "Director Delegation") => new()
    {
        Title = "Prepare deck", Description = "d", DelegationType = type,
        DoerId = "EMP-7", DoerNameSnapshot = "Dee Doer", StartDate = Start, EndDate = End, Priority = "High"
    };

    private static Task<DelegationEntity> Row(Fx f, long id) => f.Db.Delegations.AsNoTracking().SingleAsync(d => d.Id == id);

    // ---------------- delegationType ----------------
    [Fact]
    public async Task Create_AcceptsAndPersistsDelegationType_FreeText_NoCatalog()
    {
        var f = await NewAsync();

        var created = await f.Svc.CreateAsync(Request("Some Frontend Value Nobody Registered"));

        Assert.Equal("Some Frontend Value Nobody Registered", created.DelegationType);
        Assert.Equal("Some Frontend Value Nobody Registered", (await Row(f, created.DelegationId)).DelegationType);
    }

    [Theory]
    [InlineData("  Self Delegation  ", "Self Delegation")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public async Task DelegationType_IsTrimmed_AndBlankBecomesNull(string? input, string? expected)
    {
        var f = await NewAsync();

        var created = await f.Svc.CreateAsync(Request(input));

        Assert.Equal(expected, created.DelegationType);
        Assert.Equal(expected, (await Row(f, created.DelegationId)).DelegationType);
    }

    [Fact]
    public async Task DelegationType_Exactly200_IsAccepted_201_IsRejected_AndNothingIsCreated()
    {
        var f = await NewAsync();

        var ok = await f.Svc.CreateAsync(Request(new string('x', 200)));
        Assert.Equal(200, ok.DelegationType!.Length);

        await Assert.ThrowsAsync<BadRequestException>(() => f.Svc.CreateAsync(Request(new string('x', 201))));
        Assert.Equal(1, await f.Db.Delegations.CountAsync());
    }

    [Fact]
    public async Task Get_ReturnsDelegationType()
    {
        var f = await NewAsync();
        var created = await f.Svc.CreateAsync(Request("Self Delegation"));

        var detail = await f.Svc.GetByIdAsync(created.DelegationId);

        Assert.Equal("Self Delegation", detail.DelegationType);
    }

    [Fact]
    public async Task Update_ChangesDelegationType_AndBlankClearsIt()
    {
        var f = await NewAsync();
        var created = await f.Svc.CreateAsync(Request("Self Delegation"));

        var updated = await f.Svc.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto { Title = "t", DoerId = "EMP-7", DelegationType = "  Director Delegation " });
        Assert.Equal("Director Delegation", updated.DelegationType);
        Assert.Equal("Director Delegation", (await Row(f, created.DelegationId)).DelegationType);

        var cleared = await f.Svc.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto { Title = "t", DoerId = "EMP-7", DelegationType = " " });
        Assert.Null(cleared.DelegationType);
    }

    [Fact]
    public async Task DelegationType_IsReturnedByList_StartAndLifecycleResponses_AndFilterable()
    {
        var f = await NewAsync();
        var a = await f.Svc.CreateAsync(Request("Self Delegation"));
        await f.Svc.CreateAsync(Request("Director Delegation"));
        await f.Svc.CreateAsync(Request(null));

        var all = await f.Svc.ListAsync(new DelegationListQueryDto());
        Assert.Equal(new[] { "Director Delegation", "Self Delegation" }, all.Items.Select(i => i.DelegationType).Where(t => t != null).OrderBy(t => t));

        var self = await f.Svc.ListAsync(new DelegationListQueryDto { DelegationType = "  self delegation " });
        Assert.Equal(a.DelegationId, Assert.Single(self.Items).DelegationId);

        var started = await f.Svc.StartAsync(a.DelegationId);
        Assert.Equal("Self Delegation", started.DelegationType);
        var completed = await f.Svc.CompleteAsync(a.DelegationId, null);
        Assert.Equal("Self Delegation", completed.DelegationType);
    }

    // ---------------- startDate / endDate ----------------
    [Fact]
    public async Task StartDate_Persists_IsReturned_AndIsNotStartedAt()
    {
        var f = await NewAsync();

        var created = await f.Svc.CreateAsync(Request());

        Assert.Equal(Start, created.StartDate);
        Assert.Null(created.StartedAt);
        var row = await Row(f, created.DelegationId);
        Assert.Equal(Start, row.StartDate);
        Assert.Null(row.StartedAt);
        Assert.Equal(Start, (await f.Svc.GetByIdAsync(created.DelegationId)).StartDate);
        Assert.Equal(Start, Assert.Single((await f.Svc.ListAsync(new DelegationListQueryDto())).Items).StartDate);
    }

    [Fact]
    public async Task Start_IsNotGatedByStartDate_AndDoesNotOverwriteIt()
    {
        var f = await NewAsync();
        var future = DateTime.UtcNow.AddDays(30);
        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "EMP-7", StartDate = future });

        var started = await f.Svc.StartAsync(created.DelegationId);

        Assert.Equal(DelegationStatus.InProgress, started.Status);
        Assert.NotNull(started.StartedAt);
        Assert.Equal(future, started.StartDate);
        Assert.NotEqual(started.StartDate, started.StartedAt);
    }

    [Fact]
    public async Task Update_ChangesStartDateAndEndDate_AndAllowsThemToBeCleared()
    {
        var f = await NewAsync();
        var created = await f.Svc.CreateAsync(Request());

        var moved = await f.Svc.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto { Title = "t", DoerId = "EMP-7", StartDate = Start.AddDays(1), EndDate = End.AddDays(1) });
        Assert.Equal((Start.AddDays(1), End.AddDays(1)), (moved.StartDate, moved.EndDate));

        var cleared = await f.Svc.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto { Title = "t", DoerId = "EMP-7" });
        Assert.Equal((null, null), (cleared.StartDate, cleared.EndDate));
        var row = await Row(f, created.DelegationId);
        Assert.Null(row.StartDate);
        Assert.Null(row.DueDate);
    }

    [Fact]
    public async Task EndDate_IsStoredInTheExistingDueDateColumn_AndDrivesTheDueCalculations()
    {
        var f = await NewAsync();

        var created = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t", DoerId = "EMP-7", EndDate = IndiaBusinessCalendar.Today.AddDays(-2) });

        Assert.Equal(created.EndDate, (await Row(f, created.DelegationId)).DueDate);
        Assert.True(created.IsOverdue);
        Assert.False(created.IsDueToday);
        Assert.Equal(1, (await f.Svc.GetSummaryAsync()).Overdue);

        var dueToday = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "t2", DoerId = "EMP-7", EndDate = IndiaBusinessCalendar.Today });
        Assert.True(dueToday.IsDueToday);
        Assert.Equal(1, (await f.Svc.GetSummaryAsync()).DueToday);
    }

    [Fact]
    public async Task ListFilter_EndDate_MatchesTheDay_OnTheDueDateColumn()
    {
        var f = await NewAsync();
        var a = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "a", DoerId = "E1", EndDate = End });
        await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "b", DoerId = "E1", EndDate = End.AddDays(3) });

        var page = await f.Svc.ListAsync(new DelegationListQueryDto { EndDate = End.AddHours(6) });

        Assert.Equal(a.DelegationId, Assert.Single(page.Items).DelegationId);
    }

    // ---------------- doer ----------------
    [Fact]
    public async Task DoerFields_MapToTheDoerColumns_AndBackToTheResponse()
    {
        var f = await NewAsync();

        var created = await f.Svc.CreateAsync(Request());

        var row = await Row(f, created.DelegationId);
        Assert.Equal("EMP-7", row.DoerId);
        Assert.Equal("Dee Doer", row.DoerNameSnapshot);
        Assert.Equal("EMP-7", created.DoerId);
        Assert.Equal("Dee Doer", created.DoerName);
        // The delegator stays server-owned and separately named.
        Assert.Equal("ea-actor", row.AssignedById); // same server actor convention as before
        Assert.Equal("ea-actor", row.AssignedByNameSnapshot);
        Assert.Equal((row.AssignedById, row.AssignedByNameSnapshot), (created.AssignedById, created.AssignedByName));
        Assert.NotEqual(created.DoerId, created.AssignedById);
    }

    [Fact]
    public async Task Update_MapsDoerFields_AndKeepsTheDelegatorUnchanged()
    {
        var f = await NewAsync();
        var created = await f.Svc.CreateAsync(Request());

        var updated = await f.Svc.UpdateAsync(created.DelegationId, new DelegationUpdateRequestDto { Title = "t", DoerId = " EMP-9 ", DoerNameSnapshot = " New Doer " });

        Assert.Equal(("EMP-9", "New Doer"), (updated.DoerId, updated.DoerName));
        var row = await Row(f, created.DelegationId);
        Assert.Equal(("EMP-9", "New Doer"), (row.DoerId, row.DoerNameSnapshot));
        Assert.Equal(("ea-actor", "ea-actor"), (row.AssignedById, row.AssignedByNameSnapshot));
    }

    [Fact]
    public async Task DoerNameSnapshot_IsOptional_AndListFilterUsesDoerId()
    {
        var f = await NewAsync();
        var mine = await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "mine", DoerId = "EMP-1" });
        await f.Svc.CreateAsync(new DelegationCreateRequestDto { Title = "other", DoerId = "EMP-2", DoerNameSnapshot = "Other" });

        Assert.Null(mine.DoerName);
        var page = await f.Svc.ListAsync(new DelegationListQueryDto { DoerId = " EMP-1 " });

        Assert.Equal(mine.DelegationId, Assert.Single(page.Items).DelegationId);
    }

    [Fact]
    public async Task InternalCommand_UsesDoerAndDueDateNames_MeetingCreatedDelegationHasNullTypeAndStartDate()
    {
        var f = await NewAsync();

        var created = await f.Svc.CreateCoreAsync(new DelegationCreateCommand
        {
            Title = "From meeting", DoerId = "EMP-5", DoerNameSnapshot = "Owner", DueDate = End,
            SourceEntityId = "77", SourceReference = "MTG-1"
        }, default);

        Assert.Null(created.DelegationType);
        Assert.Null(created.StartDate);
        var row = await Row(f, created.DelegationId);
        Assert.Equal(("EMP-5", "Owner", End), (row.DoerId, row.DoerNameSnapshot, row.DueDate));
        Assert.Null(row.DelegationType);
        Assert.Null(row.StartDate);
        Assert.Equal((created.EndDate, created.DoerId, created.DoerName), (End, "EMP-5", "Owner"));
    }

    // ---------------- public surface ----------------
    private static string[] Names(Type t) => t.GetProperties().Select(p => p.Name).ToArray();

    [Fact]
    public void CreateAndUpdateContracts_ExposeTheNewNames_AndNotTheOldOnes()
    {
        foreach (var dto in new[] { typeof(DelegationCreateRequestDto), typeof(DelegationUpdateRequestDto) })
        {
            var names = Names(dto);
            Assert.Contains("DelegationType", names);
            Assert.Contains("DoerId", names);
            Assert.Contains("DoerNameSnapshot", names);
            Assert.Contains("StartDate", names);
            Assert.Contains("EndDate", names);
            Assert.DoesNotContain("AssignedToId", names);
            Assert.DoesNotContain("AssignedToNameSnapshot", names);
            Assert.DoesNotContain("DueDate", names);
            Assert.Equal(200, dto.GetProperty("DelegationType")!.GetCustomAttribute<MaxLengthAttribute>()!.Length);
            Assert.All(new[] { "DelegationType", "DoerId", "DoerNameSnapshot", "StartDate", "EndDate" },
                n => Assert.True(new NullabilityInfoContext().Create(dto.GetProperty(n)!).WriteState != NullabilityState.NotNull, n));
        }
    }

    [Fact]
    public void ResponseAndListQuery_UseTheNewNames_AndKeepTheDelegatorNames()
    {
        var response = Names(typeof(DelegationResponseDto));
        foreach (var n in new[] { "DelegationType", "DoerId", "DoerName", "StartDate", "EndDate", "StartedAt", "AssignedById", "AssignedByName", "CompletionPdfAttachmentId" })
            Assert.Contains(n, response);
        Assert.DoesNotContain("AssignedToId", response);
        Assert.DoesNotContain("AssignedToName", response);
        Assert.DoesNotContain("DueDate", response);

        var query = Names(typeof(DelegationListQueryDto));
        Assert.Contains("DoerId", query);
        Assert.Contains("EndDate", query);
        Assert.Contains("DelegationType", query);
        Assert.DoesNotContain("AssignedToId", query);
        Assert.DoesNotContain("DueDate", query);
    }

    [Fact]
    public void DatabaseModel_UsesDoerColumns_KeepsDueDate_AndHasNoAssignedToOrEndDateColumns()
    {
        using var db = new EaFmsDbContext(new DbContextOptionsBuilder<EaFmsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var entity = db.Model.FindEntityType(typeof(DelegationEntity))!;
        var columns = entity.GetProperties().Select(p => p.GetColumnName()).ToList();

        Assert.Contains("DelegationType", columns);
        Assert.Contains("StartDate", columns);
        Assert.Contains("DueDate", columns);            // endDate is still persisted in DueDate
        Assert.Contains("DoerId", columns);
        Assert.Contains("DoerNameSnapshot", columns);
        Assert.Contains("AssignedById", columns);
        Assert.DoesNotContain(columns, c => c is "EndDate" or "AssignedToId" or "AssignedToNameSnapshot");
        Assert.Equal(200, entity.FindProperty("DelegationType")!.GetMaxLength());
        Assert.True(entity.FindProperty("DelegationType")!.IsNullable);
        Assert.True(entity.FindProperty("StartDate")!.IsNullable);
        Assert.Contains(entity.GetIndexes(), i => i.Properties.Single().Name == "DoerId");
    }
}
