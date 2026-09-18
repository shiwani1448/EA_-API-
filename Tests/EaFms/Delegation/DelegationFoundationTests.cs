using System;
using System.Linq;
using System.Threading.Tasks;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jarvis5.Tests.EaFms.Delegation;

/// <summary>
/// Step 1 persistence-foundation tests only — no DelegationService/controller exists yet.
/// These verify the entity/EF configuration round-trips correctly and that the
/// Delegation/EaTask/SourceEntity/ReferenceNo identities stay distinct, matching the
/// same separation already established for Travel and Approval.
/// </summary>
public class DelegationFoundationTests
{
    private static EaFmsDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(BusinessModule module, EaTask task)> SeedModuleAndTaskAsync(EaFmsDbContext db, string moduleName = "Delegation")
    {
        var module = new BusinessModule { Name = moduleName, IsActive = true, IsDeleted = false,
            CreatedBy = "tester", CreatedDate = DateTime.UtcNow };
        db.BusinessModules.Add(module);
        await db.SaveChangesAsync();

        var task = new EaTask { BusinessModuleId = module.Id, ModuleName = module.Name, BusinessRecordId = "1", Task = "DLG-2026-000001",
            ExecutionStatus = "NotStarted", IsActive = true, CreatedBy = "tester", CreatedDate = DateTime.UtcNow };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return (module, task);
    }

    [Fact]
    public async Task Delegation_PersistsAndRoundTripsAllRequiredFields()
    {
        await using var db = MakeDb();
        var (module, task) = await SeedModuleAndTaskAsync(db);
        var (sourceModule, _) = await SeedModuleAndTaskAsync(db, "Meeting");

        var delegation = new Jarvis5.Entities.EaFms.Delegation
        {
            ReferenceNo = "DLG-2026-000001",
            EaTaskId = task.Id,
            Title = "Prepare board deck",
            Description = "Compile Q3 numbers",
            AssignedToId = "emp-42",
            AssignedToNameSnapshot = "Doer Name",
            AssignedById = "mgr-7",
            AssignedByNameSnapshot = "Manager Name",
            Priority = "High",
            DueDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            SourceBusinessModuleId = sourceModule.Id,
            SourceEntityId = "51",
            SourceReference = "MTG-000051",
            AdditionalNotes = "Coordinate with finance",
            CreatedBy = "mgr-7",
            CreatedDate = DateTime.UtcNow
        };
        db.Delegations.Add(delegation);
        await db.SaveChangesAsync();

        var reloaded = await db.Delegations.AsNoTracking().SingleAsync(d => d.Id == delegation.Id);

        Assert.Equal("DLG-2026-000001", reloaded.ReferenceNo);
        Assert.Equal(task.Id, reloaded.EaTaskId);
        Assert.Equal("Pending", reloaded.Status); // CLR default, no service has run yet
        Assert.Equal("emp-42", reloaded.AssignedToId);
        Assert.Equal("mgr-7", reloaded.AssignedById);
        Assert.Equal(sourceModule.Id, reloaded.SourceBusinessModuleId);
        Assert.Equal("51", reloaded.SourceEntityId);
        Assert.Equal("MTG-000051", reloaded.SourceReference);
        Assert.Null(reloaded.StartedAt);
        Assert.Null(reloaded.CompletedAt);
        Assert.False(reloaded.IsDeleted);
    }

    [Fact]
    public async Task Delegation_IdentitiesRemainDistinct_NoneSubstitutedForAnother()
    {
        // Regression guard: Id (API/DB identity), ReferenceNo (display), EaTaskId (central
        // task), and SourceEntityId (originating record) must never collapse into each other.
        await using var db = MakeDb();
        var (module, task) = await SeedModuleAndTaskAsync(db);
        var (sourceModule, _) = await SeedModuleAndTaskAsync(db, "Travel & Hospitality");

        var delegation = new Jarvis5.Entities.EaFms.Delegation
        {
            ReferenceNo = "DLG-2026-000002",
            EaTaskId = task.Id,
            Title = "Follow up on travel booking",
            AssignedToId = "emp-1",
            AssignedById = "mgr-1",
            SourceBusinessModuleId = sourceModule.Id,
            SourceEntityId = "999",
            CreatedBy = "mgr-1",
            CreatedDate = DateTime.UtcNow
        };
        db.Delegations.Add(delegation);
        await db.SaveChangesAsync();

        Assert.NotEqual(0, delegation.Id);
        // EaTaskId correctly references the EaTask row's own PK, not Delegation's PK —
        // a separate auto-increment column in a separate table (any numeric coincidence
        // between them is irrelevant to this invariant).
        Assert.Equal(task.Id, delegation.EaTaskId);
        Assert.NotEqual(delegation.Id.ToString(), delegation.ReferenceNo); // Id is not the display reference
        Assert.NotEqual(delegation.SourceEntityId, delegation.Id.ToString()); // source record identity is independent
    }

    [Fact]
    public async Task Delegation_EaTaskAndSourceBusinessModule_NavigationsResolve()
    {
        await using var db = MakeDb();
        var (module, task) = await SeedModuleAndTaskAsync(db, "Delegation");
        var (sourceModule, _) = await SeedModuleAndTaskAsync(db, "EA Approval");

        var delegation = new Jarvis5.Entities.EaFms.Delegation
        {
            ReferenceNo = "DLG-2026-000003",
            EaTaskId = task.Id,
            Title = "Chase approval decision",
            AssignedToId = "emp-2",
            AssignedById = "mgr-2",
            SourceBusinessModuleId = sourceModule.Id,
            SourceEntityId = "APR-2026-000010",
            CreatedBy = "mgr-2",
            CreatedDate = DateTime.UtcNow
        };
        db.Delegations.Add(delegation);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.Delegations
            .Include(d => d.EaTask)
            .Include(d => d.SourceBusinessModule)
            .SingleAsync(d => d.Id == delegation.Id);

        Assert.Equal(task.Id, reloaded.EaTask.Id);
        Assert.Equal(module.Id, reloaded.EaTask.BusinessModuleId);
        Assert.Equal("EA Approval", reloaded.SourceBusinessModule.Name);
    }

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("InProgress", true)]
    [InlineData("Completed", true)]
    [InlineData("Cancelled", false)]
    [InlineData("DueToday", false)]
    [InlineData("Overdue", false)]
    public void DelegationStatus_OnlyPersistsTheThreeCoreLifecycleValues(string status, bool expected)
    {
        // DueToday/Overdue are time-derived views, never persisted statuses (per spec);
        // Cancelled is not part of the current frontend contract and was not added.
        Assert.Equal(expected, DelegationStatus.IsStatus(status));
    }

    [Fact]
    public void DelegationStatus_DefaultConstant_IsPending()
    {
        Assert.Equal("Pending", DelegationStatus.Pending);
        Assert.Equal("InProgress", DelegationStatus.InProgress);
        Assert.Equal("Completed", DelegationStatus.Completed);
    }
}
