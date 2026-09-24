using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Mapping;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DelegationEntity = Jarvis5.Entities.EaFms.Delegation;

namespace Jarvis5.Tests.EaFms.Followups;

/// <summary>
/// Follow-up &amp; Escalation step 3: Followup business APIs. A Followup is an actual follow-up
/// activity against BusinessModuleId + BusinessRecordId; it is never created from an EaTask,
/// never creates an EaTask, and never triggers reminders or escalations.
/// </summary>
public class FollowupBusinessApiTests
{
    internal static readonly DateTime Base = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    internal static EaFmsDbContext MakeDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    internal static readonly IMapper Mapper =
        new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();

    private static FollowupService MakeService(EaFmsDbContext db, Mock<IAuditService>? audit = null) => new(
        new FollowupRepository(db), db, Mapper,
        Mock.Of<ICurrentUserService>(u => u.UserId == 7 && u.UserName == "EA User"),
        (audit ?? new Mock<IAuditService>()).Object, new FollowupSourceResolver(db), FollowupTestSupport.EaTasks(db), new TatRuleRepository(db));

    internal sealed class Seed
    {
        public required EaFmsDbContext Db { get; init; }
        public required Dictionary<string, BusinessModule> Modules { get; init; }
        public required Meeting Meeting { get; init; }
        public required TravelRequest Travel { get; init; }
        public required ApprovalRequest Approval { get; init; }
        public required DelegationEntity Delegation { get; init; }
    }

    /// <summary>Real source records for each supported module; module ids are not 4/5/6/7.</summary>
    internal static async Task<Seed> SeedAsync()
    {
        var db = MakeDb();
        // Shift identities so nothing can rely on the developer database's module ids.
        var modules = new[] { "Vendor Management", "Meeting", "Travel & Hospitality", "EA Approval", "Delegation" }
            .Select(n => new BusinessModule { Name = n, IsActive = true, CreatedBy = "seed", CreatedDate = Base }).ToList();
        db.BusinessModules.AddRange(modules);
        await db.SaveChangesAsync();

        var meeting = new Meeting { Title = "Board meeting", CreatedBy = "seed", CreatedDate = Base };
        var travel = new TravelRequest { ReferenceNo = "TRV-2026-000001", EaTaskId = 501, CreatedBy = "seed", CreatedDate = Base };
        var approval = new ApprovalRequest { ReferenceNo = "APR-2026-000009", EaTaskId = 502, RequestTitle = "Laptop purchase", CreatedBy = "seed" };
        var delegation = new DelegationEntity { ReferenceNo = "DLG-2026-000001", EaTaskId = 503, Title = "Chase vendor", DoerId = "u1", AssignedById = "u2", CreatedBy = "seed", CreatedDate = Base };
        db.Meetings.Add(meeting); db.TravelRequests.Add(travel); db.ApprovalRequests.Add(approval); db.Delegations.Add(delegation);
        await db.SaveChangesAsync();
        return new Seed
        {
            Db = db, Modules = modules.ToDictionary(m => m.Name), Meeting = meeting, Travel = travel,
            Approval = approval, Delegation = delegation
        };
    }

    private static CreateFollowupRequestDto Create(long? moduleId, string? recordId, string? subject = "Chase") => new()
    {
        BusinessModuleId = moduleId, BusinessRecordId = recordId, Subject = subject,
        Note = "note", Type = "Action", DueAt = Base.AddDays(2), DoerId = "ea-1", DoerName = "EA One"
    };

    // 1-4: create against each supported module, identified only by module + record.
    [Fact]
    public async Task Create_MeetingFollowup_ResolvesMeetingSource()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var mid = s.Modules["Meeting"].Id;

        var created = await MakeService(s.Db).CreateAsync(Create(mid, s.Meeting.Id.ToString()));

        Assert.Equal((mid, s.Meeting.Id.ToString()), (created.BusinessModuleId, created.BusinessRecordId));
        Assert.Equal("Meeting", created.BusinessModuleName);
        Assert.Equal("Board meeting", created.BusinessRecordTitle);
        Assert.Null(created.IntakeRequestId);
    }

    [Fact]
    public async Task Create_TravelFollowup_ResolvesTravelSource_WithoutWorkflowOrIntake()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var mid = s.Modules["Travel & Hospitality"].Id;

        var created = await MakeService(s.Db).CreateAsync(Create(mid, s.Travel.Id.ToString()));

        Assert.Equal((mid, s.Travel.Id.ToString()), (created.BusinessModuleId, created.BusinessRecordId));
        Assert.Equal("Travel & Hospitality", created.BusinessModuleName);
        Assert.Equal("TRV-2026-000001", created.BusinessRecordTitle);
        Assert.Null(created.WorkflowInstanceId);
        Assert.Null(created.IntakeRequestId);
    }

    [Fact]
    public async Task Create_ApprovalFollowup_UsesReferenceNoTheCentralTaskIdentity()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var mid = s.Modules["EA Approval"].Id;

        var created = await MakeService(s.Db).CreateAsync(Create(mid, "  APR-2026-000009 "));

        Assert.Equal((mid, "APR-2026-000009"), (created.BusinessModuleId, created.BusinessRecordId));
        Assert.Equal("Laptop purchase", created.BusinessRecordTitle);
        Assert.Null(created.WorkflowInstanceId);
    }

    [Fact]
    public async Task Create_DelegationFollowup_ResolvesDelegationSource()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var mid = s.Modules["Delegation"].Id;

        var created = await MakeService(s.Db).CreateAsync(Create(mid, s.Delegation.Id.ToString()));

        Assert.Equal((mid, s.Delegation.Id.ToString()), (created.BusinessModuleId, created.BusinessRecordId));
        Assert.Equal("Chase vendor", created.BusinessRecordTitle);
        Assert.Null(created.WorkflowInstanceId);
    }

    // Future module: validated against its central EaTask, no code change.
    [Fact]
    public async Task Create_FutureModuleFollowup_ValidatedAgainstItsEaTask()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var future = s.Modules["Vendor Management"];
        s.Db.Tasks.Add(new EaTask { BusinessModuleId = future.Id, ModuleName = future.Name, BusinessRecordId = "V-77", Task = "Onboard vendor",
            ExecutionStatus = "NotStarted", CreatedBy = "1", CreatedDate = Base });
        await s.Db.SaveChangesAsync();
        var svc = MakeService(s.Db);

        var created = await svc.CreateAsync(Create(future.Id, "V-77"));

        Assert.Equal("Onboard vendor", created.BusinessRecordTitle);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateAsync(Create(future.Id, "V-404")));
    }

    // 5-6: business source validation.
    [Fact]
    public async Task Create_UnknownOrInactiveModule_IsRejected()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var inactive = new BusinessModule { Name = "Retired", IsActive = false, CreatedBy = "seed", CreatedDate = Base };
        s.Db.BusinessModules.Add(inactive);
        await s.Db.SaveChangesAsync();
        var svc = MakeService(s.Db);

        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateAsync(Create(99999, "1")));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateAsync(Create(inactive.Id, "1")));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(0, "1")));
        Assert.Equal(0, await s.Db.Followups.CountAsync());
    }

    [Theory]
    [InlineData("Meeting", "424242")]
    [InlineData("Travel & Hospitality", "424242")]
    [InlineData("Delegation", "424242")]
    [InlineData("EA Approval", "APR-2026-999999")]
    public async Task Create_NonexistentBusinessRecord_IsRejectedWith404(string module, string recordId)
    {
        var s = await SeedAsync(); await using var _ = s.Db;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            MakeService(s.Db).CreateAsync(Create(s.Modules[module].Id, recordId)));
        Assert.Equal(0, await s.Db.Followups.CountAsync());
    }

    [Theory]
    [InlineData("Meeting")]
    [InlineData("Travel & Hospitality")]
    [InlineData("Delegation")]
    public async Task Create_NonNumericRecordIdForNumericModules_IsBadRequest(string module)
    {
        var s = await SeedAsync(); await using var _ = s.Db;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            MakeService(s.Db).CreateAsync(Create(s.Modules[module].Id, "abc")));
    }

    [Fact]
    public async Task Create_DeletedSourceRecord_IsRejected()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        s.Travel.IsDeleted = true;
        await s.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            MakeService(s.Db).CreateAsync(Create(s.Modules["Travel & Hospitality"].Id, s.Travel.Id.ToString())));
    }

    [Fact]
    public async Task Create_ModuleWithoutRecord_OrRecordWithoutModule_IsBadRequest()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var svc = MakeService(s.Db);

        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(s.Modules["Meeting"].Id, null)));
        await Assert.ThrowsAsync<BadRequestException>(() => svc.CreateAsync(Create(null, "1")));
    }

    // 7-8: IntakeRequestId / WorkflowInstanceId are optional; only DueAt-independent identity is needed.
    [Fact]
    public async Task Create_MinimalModuleFollowup_NeedsOnlyModuleAndRecord()
    {
        var s = await SeedAsync(); await using var _ = s.Db;

        var created = await MakeService(s.Db).CreateAsync(new CreateFollowupRequestDto
        {
            BusinessModuleId = s.Modules["Delegation"].Id, BusinessRecordId = s.Delegation.Id.ToString()
        });

        Assert.True(created.Id > 0);
        Assert.Null(created.Subject);
        Assert.Null(created.DoerId);
        Assert.Null(created.IntakeRequestId);
        Assert.Null(created.WorkflowInstanceId);
    }

    [Fact]
    public void CreateValidator_HasNoBackendMandatoryBusinessFields()
    {
        var result = new CreateFollowupRequestDtoValidator().Validate(new CreateFollowupRequestDto());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Create_MeetingIntakeOrWorkflowMismatch_StillRejected()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var intake = new IntakeRequest { Title = "i", CreatedBy = "seed", CreatedDate = Base };
        s.Db.IntakeRequests.Add(intake);
        await s.Db.SaveChangesAsync();

        // The meeting has no intake; supplying a different one is only rejected when the source has one.
        s.Meeting.IntakeRequestId = intake.Id;
        await s.Db.SaveChangesAsync();
        var dto = Create(s.Modules["Meeting"].Id, s.Meeting.Id.ToString());
        dto.IntakeRequestId = intake.Id + 100;

        await Assert.ThrowsAsync<NotFoundException>(() => MakeService(s.Db).CreateAsync(dto));
    }

    // 9-11: retrieval.
    [Fact]
    public async Task Get_ListFilters_ByModule_ByModuleAndRecord_AndSingle()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var svc = MakeService(s.Db);
        var m = s.Modules["Meeting"].Id;
        var t = s.Modules["Travel & Hospitality"].Id;
        var a = await svc.CreateAsync(Create(m, s.Meeting.Id.ToString(), "m1"));
        await svc.CreateAsync(Create(m, s.Meeting.Id.ToString(), "m2"));
        await svc.CreateAsync(Create(t, s.Travel.Id.ToString(), "t1"));

        var all = await svc.GetPagedAsync(new FollowupListQueryDto());
        var byModule = await svc.GetPagedAsync(new FollowupListQueryDto { BusinessModuleId = m });
        var bySource = await svc.GetPagedAsync(new FollowupListQueryDto { BusinessModuleId = t, BusinessRecordId = s.Travel.Id.ToString() });
        var none = await svc.GetPagedAsync(new FollowupListQueryDto { BusinessModuleId = t, BusinessRecordId = "999" });
        var single = await svc.GetByIdAsync(a.Id);

        Assert.Equal(3, all.TotalCount);
        Assert.Equal(2, byModule.TotalCount);
        Assert.All(byModule.Items, i => Assert.Equal(m, i.BusinessModuleId));
        Assert.Equal("t1", Assert.Single(bySource.Items).Subject);
        Assert.Empty(none.Items);
        Assert.Equal("m1", single.Subject);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetByIdAsync(99999));
    }

    // 12-13: update keeps source identity.
    [Fact]
    public async Task Update_ChangesBusinessFields_ButNeverTheSourceIdentity()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var svc = MakeService(s.Db);
        var mid = s.Modules["Travel & Hospitality"].Id;
        var created = await svc.CreateAsync(Create(mid, s.Travel.Id.ToString()));

        // A client that echoes the source fields back in the PUT body cannot move the follow-up.
        var dto = JsonSerializer.Deserialize<UpdateFollowupRequestDto>(
            $$"""{"subject":"Updated","note":"n2","dueAt":"2026-11-01T10:00:00Z","businessModuleId":{{s.Modules["Meeting"].Id}},"businessRecordId":"{{s.Meeting.Id}}","intakeRequestId":5,"workflowInstanceId":9}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var updated = await svc.UpdateAsync(created.Id, dto);

        Assert.Equal("Updated", updated.Subject);
        Assert.Equal("n2", updated.Note);
        Assert.Equal(new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc), updated.DueAt);
        Assert.Equal((mid, s.Travel.Id.ToString()), (updated.BusinessModuleId, updated.BusinessRecordId));
        Assert.Null(updated.IntakeRequestId);
        Assert.Null(updated.WorkflowInstanceId);
        var row = await s.Db.Followups.AsNoTracking().SingleAsync();
        Assert.Equal((mid, s.Travel.Id.ToString()), (row.BusinessModuleId, row.BusinessRecordId));
        Assert.Equal("EA User", row.ModifiedBy);
    }

    [Fact]
    public void UpdateDto_ExposesNoSourceIdentityFields()
    {
        var props = typeof(UpdateFollowupRequestDto).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("BusinessModuleId", props);
        Assert.DoesNotContain("BusinessRecordId", props);
        Assert.DoesNotContain("IntakeRequestId", props);
        Assert.DoesNotContain("WorkflowInstanceId", props);
    }

    [Fact]
    public async Task Update_UnknownFollowup_Is404()
    {
        await using var db = MakeDb();

        await Assert.ThrowsAsync<NotFoundException>(() => MakeService(db).UpdateAsync(12345, new UpdateFollowupRequestDto()));
    }

    // 14-15: completion lifecycle.
    [Fact]
    public async Task Complete_SetsServerControlledCompletion_AndRejectsRepeat()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var svc = MakeService(s.Db);
        var created = await svc.CreateAsync(Create(s.Modules["EA Approval"].Id, "APR-2026-000009"));

        await svc.StartAsync(created.Id);
        var done = await svc.CompleteAsync(created.Id, new CompleteFollowupRequestDto { CompletionNote = "sorted", OutcomeCode = "RESOLVED" });

        Assert.True(done.IsCompleted);
        Assert.NotNull(done.CompletedAt);
        Assert.False(done.IsOverdue);
        Assert.Equal(("sorted", "RESOLVED", "7", "EA User"),
            (done.CompletionNote, done.OutcomeCode, done.CompletedById, done.CompletedByName));
        Assert.Equal("APR-2026-000009", done.BusinessRecordId);

        var repeat = await Assert.ThrowsAsync<BusinessRuleException>(() => svc.CompleteAsync(created.Id, new CompleteFollowupRequestDto()));   // 409 invalid state
        Assert.Contains("already completed", repeat.Message);
        Assert.Equal(done.CompletedAt, (await svc.GetByIdAsync(created.Id)).CompletedAt);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CompleteAsync(99999, new CompleteFollowupRequestDto()));
    }

    [Fact]
    public async Task Complete_FiltersByStatusAfterCompletion()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var svc = MakeService(s.Db);
        var mid = s.Modules["Delegation"].Id;
        var a = await svc.CreateAsync(Create(mid, s.Delegation.Id.ToString(), "a"));
        await svc.CreateAsync(Create(mid, s.Delegation.Id.ToString(), "b"));
        await svc.StartAsync(a.Id);
        await svc.CompleteAsync(a.Id, new CompleteFollowupRequestDto());

        var completed = await svc.GetPagedAsync(new FollowupListQueryDto { Status = "Completed" });
        var pending = await svc.GetPagedAsync(new FollowupListQueryDto { Status = "Pending" });

        Assert.Equal("a", Assert.Single(completed.Items).Subject);
        Assert.Equal("b", Assert.Single(pending.Items).Subject);
    }

    // Audit reuses the existing infrastructure.
    [Fact]
    public async Task CreateUpdateComplete_WriteExistingFollowupAuditEvents()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var audit = new Mock<IAuditService>();
        var svc = MakeService(s.Db, audit);
        var created = await svc.CreateAsync(Create(s.Modules["Meeting"].Id, s.Meeting.Id.ToString()));
        await svc.UpdateAsync(created.Id, new UpdateFollowupRequestDto { Subject = "x", DueAt = Base });
        await svc.StartAsync(created.Id);
        await svc.CompleteAsync(created.Id, new CompleteFollowupRequestDto());

        foreach (var action in new[] { "FOLLOWUP_CREATE", "FOLLOWUP_UPDATE", "FOLLOWUP_COMPLETE" })
            audit.Verify(a => a.AddAudit(action, "Followup", nameof(Followup), created.Id.ToString(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
    }

    // 16, 17, 19, 20: no side effects on other structures.
    [Fact]
    public async Task Create_OnlyCreatesItsOwnFollowupTask_NoNotification_OrEscalation()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var tasksBefore = await s.Db.Tasks.CountAsync();
        var svc = MakeService(s.Db);

        var f = await svc.CreateAsync(Create(s.Modules["Travel & Hospitality"].Id, s.Travel.Id.ToString()));
        await svc.UpdateAsync(f.Id, new UpdateFollowupRequestDto { Subject = "u", DueAt = Base, ReminderAt = Base.AddHours(-1) });
        await svc.StartAsync(f.Id);
        await svc.CompleteAsync(f.Id, new CompleteFollowupRequestDto());

        Assert.Equal(tasksBefore + 1, await s.Db.Tasks.CountAsync());
        Assert.Equal("Completed", (await s.Db.Tasks.SingleAsync(t => t.Id == f.EaTaskId)).ExecutionStatus);
        Assert.Equal(0, await s.Db.Notifications.CountAsync());
        Assert.Equal(0, await s.Db.Escalations.CountAsync());
        Assert.Null((await s.Db.Followups.SingleAsync()).LastFollowupAt);
    }

    [Fact]
    public async Task Workspace_StillCreatesNoFollowups_AndFollowupsDoNotChangeTheWorkspace()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var m = s.Modules["Meeting"];
        s.Db.Tasks.Add(new EaTask { BusinessModuleId = m.Id, ModuleName = m.Name, BusinessRecordId = s.Meeting.Id.ToString(), Task = "Board meeting",
            ExecutionStatus = "NotStarted", CreatedBy = "1", CreatedDate = Base });
        await s.Db.SaveChangesAsync();
        var tasks = new EaTaskService(s.Db, new EaTaskRepository(s.Db), new TatRuleRepository(s.Db), new CreateEaTaskDtoValidator(),
            Mock.Of<ICurrentUserService>(u => u.UserId == 1), Mock.Of<IAuditService>());

        var before = await tasks.QueryWorkspaceAsync(new EaTaskWorkspaceQueryDto(), default);
        Assert.Equal(0, await s.Db.Followups.CountAsync());   // a task alone implies no follow-up

        await MakeService(s.Db).CreateAsync(Create(m.Id, s.Meeting.Id.ToString()));
        await MakeService(s.Db).CreateAsync(Create(m.Id, s.Meeting.Id.ToString()));
        var after = await tasks.QueryWorkspaceAsync(new EaTaskWorkspaceQueryDto(), default);

        Assert.Equal(1, before.TotalCount);
        // The Meeting's own task row is unchanged; each follow-up adds only its own "Follow-up" task.
        Assert.Equal(1, after.Items.Count(t => t.ModuleName == "Meeting"));
        Assert.Equal(2, after.Items.Count(t => t.ModuleName == "Follow-up"));
        Assert.Equal(2, await s.Db.Followups.CountAsync());
    }

    // 18: existing intake follow-ups are untouched.
    [Fact]
    public async Task Create_IntakeFollowup_WithoutBusinessSource_StillWorks()
    {
        await using var db = MakeDb();
        var intake = new IntakeRequest { Title = "Intake", CreatedBy = "seed", CreatedDate = Base };
        db.IntakeRequests.Add(intake);
        await db.SaveChangesAsync();
        var svc = MakeService(db);

        var created = await svc.CreateAsync(new CreateFollowupRequestDto { IntakeRequestId = intake.Id, DueAt = Base, Subject = "call" });
        var byIntake = await svc.GetByIntakeRequestIdAsync(intake.Id);

        Assert.Equal(intake.Id, created.IntakeRequestId);
        Assert.Null(created.BusinessModuleId);
        Assert.Equal("call", Assert.Single(byIntake).Subject);
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateAsync(new CreateFollowupRequestDto { IntakeRequestId = 9999, DueAt = Base }));
    }

    [Fact]
    public async Task Create_StandaloneFollowup_WithNoSource_StillWorks()
    {
        await using var db = MakeDb();

        var created = await MakeService(db).CreateAsync(new CreateFollowupRequestDto { Subject = "standalone", DueAt = Base });

        Assert.Null(created.BusinessModuleId);
        Assert.Null(created.BusinessRecordId);
    }

    // Resolver: display lookups.
    [Fact]
    public async Task Resolver_ResolveAsync_ReturnsNamesAndTitlesForEverySupportedModule()
    {
        var s = await SeedAsync(); await using var _ = s.Db;
        var resolver = new FollowupSourceResolver(s.Db);

        var meeting = await resolver.ResolveAsync(s.Modules["Meeting"].Id, s.Meeting.Id.ToString());
        var approval = await resolver.ResolveAsync(s.Modules["EA Approval"].Id, "APR-2026-000009");
        var missing = await resolver.ResolveAsync(s.Modules["Delegation"].Id, "not-a-number");   // display never throws

        Assert.Equal(("Meeting", "Meeting", "Board meeting"), (meeting.BusinessModuleCode, meeting.BusinessModuleName, meeting.BusinessRecordTitle));
        Assert.Equal(("EA Approval", "Laptop purchase"), (approval.BusinessModuleName, approval.BusinessRecordTitle));
        Assert.Null(missing.BusinessRecordTitle);
        Assert.Equal(s.Modules["Meeting"].Id, await resolver.GetMeetingModuleIdAsync());
    }
}
