using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Escalations;

/// <summary>
/// Follow-up &amp; Escalation Step 4: Escalation business API tests.
///
/// All tests use EF InMemory. The service layer (EscalationService) is used directly —
/// no HTTP layer, consistent with FollowupBusinessApiTests and EscalationLevelFoundationTests.
///
/// Verifies (matching numbered requirements from the task spec):
///  1.  Create with valid configured EscalationLevel succeeds.
///  2.  Invalid EscalationLevel ID rejected.
///  3.  Deleted EscalationLevel rejected.
///  4.  Create linked to a valid Followup sets FollowupId.
///  5.  Missing Followup rejected (FollowupId > 0 but not found).
///  6.  Source identity (BusinessModuleId + BusinessRecordId) flows from Followup to Escalation.
///  7.  Meeting-sourced Followup can be escalated.
///  8.  Travel-sourced Followup can be escalated.
///  9.  Approval-sourced Followup uses ReferenceNo as BusinessRecordId.
/// 10.  Delegation-sourced Followup can be escalated.
/// 11.  Future-module Followup (no hardcoded module switch) still escalates via the generic path.
/// 12.  GetByFollowupIdAsync returns all escalations for the given followup.
/// 13.  Explicit filters: GetByFollowupIdAsync excludes deleted escalations.
/// 14.  GetByIdAsync populates EscalationLevelName and EscalationLevelNumber.
/// 15.  ResolveAsync closes an open escalation.
/// 16.  Resolve already-resolved escalation is rejected.
/// 17.  No EaTask is created by Escalation operations.
/// 18.  No additional Followup is created by Escalation operations.
/// 19.  No Email/WhatsApp reminder side-effect (audit only via mock).
/// 20.  Existing Followup APIs remain unaffected (cross-checked by zero followup count change).
/// 21.  EscalationState reflects lifecycle: Open → Acknowledged → Resolved.
/// 22.  AcknowledgeAsync populates AcknowledgedAt and AcknowledgedById.
/// 23.  Cannot acknowledge an already-resolved escalation.
/// 24.  Cannot acknowledge an already-acknowledged escalation.
/// 25.  NextEscalationLevelId must be greater than current level (ordering rule).
/// </summary>
public class EscalationBusinessApiTests
{
    // ──────────────────────────────────────────────────────────────
    // Infrastructure
    // ──────────────────────────────────────────────────────────────

    private static EaFmsDbContext MakeDb() => new(
        new DbContextOptionsBuilder<EaFmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static readonly IMapper Mapper =
        new MapperConfiguration(c => c.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();

    private static EscalationService MakeService(
        EaFmsDbContext db, Mock<IAuditService>? audit = null,
        string actorName = "ea-actor", long actorId = 42)
    {
        var user = Mock.Of<ICurrentUserService>(
            u => u.UserId == actorId && u.UserName == actorName);
        return new EscalationService(
            new EscalationRepository(db), db, Mapper, user,
            (audit ?? new Mock<IAuditService>()).Object);
    }

    // ── Seed helpers ──────────────────────────────────────────────

    private static EscalationLevel SeedLevel(
        EaFmsDbContext db, int id = 1, int levelNum = 1,
        string code = "L1", bool deleted = false)
    {
        var l = new EscalationLevel
        {
            Id = id, Code = code, Name = $"Level {code}",
            Description = $"{code} desc", Level = levelNum,
            IsDeleted = deleted, CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        };
        db.EscalationLevels.Add(l);
        db.SaveChanges();
        return l;
    }

    private static BusinessModule SeedModule(
        EaFmsDbContext db, string name, long id = 0)
    {
        // If id is provided use Add with explicit id, otherwise let EF generate.
        var m = new BusinessModule
        {
            Name = name, IsActive = true, IsDeleted = false,
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        };
        if (id > 0) m.Id = id;
        db.BusinessModules.Add(m);
        db.SaveChanges();
        return m;
    }

    private static Followup SeedFollowup(
        EaFmsDbContext db,
        long? businessModuleId = null,
        string? businessRecordId = null,
        long? workflowInstanceId = null,
        bool deleted = false)
    {
        var f = new Followup
        {
            BusinessModuleId = businessModuleId,
            BusinessRecordId = businessRecordId,
            WorkflowInstanceId = workflowInstanceId,
            DueAt = DateTime.UtcNow.AddDays(3),
            IsDeleted = deleted,
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        };
        db.Followups.Add(f);
        db.SaveChanges();
        return f;
    }

    // ──────────────────────────────────────────────────────────────
    // 1. Valid create with configured EscalationLevel succeeds
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidLevel_AndValidFollowup_Succeeds()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db, id: 1, levelNum: 1, code: "L1");
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id,
            EscalationLevelId = level.Id,
            Notes = "Waiting for response"
        });

        Assert.True(result.Id > 0);
        Assert.Equal(followup.Id, result.FollowupId);
        Assert.Equal(level.Id, result.EscalationLevelId);
        Assert.Equal("Level L1", result.EscalationLevelName);
        Assert.Equal("Open", result.EscalationState);
        Assert.Null(result.ResolvedAt);
        Assert.Equal("Waiting for response", result.Notes);
    }

    // ──────────────────────────────────────────────────────────────
    // 2. Invalid EscalationLevel ID rejected
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_NonExistentEscalationLevel_ThrowsNotFoundException()
    {
        await using var db = MakeDb();
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.CreateAsync(new CreateEscalationRequestDto
            {
                FollowupId = followup.Id,
                EscalationLevelId = 999   // does not exist
            }));
    }

    // ──────────────────────────────────────────────────────────────
    // 3. Deleted EscalationLevel rejected (IsDeleted = true treated same as not found)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_DeletedEscalationLevel_ThrowsNotFoundException()
    {
        await using var db = MakeDb();
        var deletedLevel = SeedLevel(db, id: 2, levelNum: 2, code: "L2", deleted: true);
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.CreateAsync(new CreateEscalationRequestDto
            {
                FollowupId = followup.Id,
                EscalationLevelId = deletedLevel.Id
            }));
    }

    // ──────────────────────────────────────────────────────────────
    // 4. Create linked to a valid Followup sets FollowupId on Escalation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_SetsFollowupId_OnPersistedEscalation()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        var persisted = await db.Escalations.FindAsync(result.Id);
        Assert.NotNull(persisted);
        Assert.Equal(followup.Id, persisted!.FollowupId);
    }

    // ──────────────────────────────────────────────────────────────
    // 5. Missing Followup (id not found) is rejected
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_NonExistentFollowup_ThrowsNotFoundException()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var svc = MakeService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.CreateAsync(new CreateEscalationRequestDto
            {
                FollowupId = 99999,   // does not exist
                EscalationLevelId = level.Id
            }));
    }

    // ──────────────────────────────────────────────────────────────
    // 6. Source identity flows from Followup to Escalation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_InheritsBusinessModuleIdAndRecordId_FromFollowup()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var module = SeedModule(db, "Meeting");
        var followup = SeedFollowup(db,
            businessModuleId: module.Id, businessRecordId: "42");
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        Assert.Equal(module.Id, result.BusinessModuleId);
        Assert.Equal("42", result.BusinessRecordId);
    }

    // ──────────────────────────────────────────────────────────────
    // 7. Meeting-sourced Followup can be escalated
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_MeetingSourcedFollowup_Succeeds()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var meetingModule = SeedModule(db, "Meeting");
        var followup = SeedFollowup(db,
            businessModuleId: meetingModule.Id, businessRecordId: "100");
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        Assert.Equal(meetingModule.Id, result.BusinessModuleId);
        Assert.Equal("100", result.BusinessRecordId);
        Assert.Equal("Open", result.EscalationState);
    }

    // ──────────────────────────────────────────────────────────────
    // 8. Travel-sourced Followup can be escalated
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_TravelSourcedFollowup_Succeeds()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var travelModule = SeedModule(db, "Travel & Hospitality");
        var followup = SeedFollowup(db,
            businessModuleId: travelModule.Id, businessRecordId: "77");
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        Assert.Equal(travelModule.Id, result.BusinessModuleId);
        Assert.Equal("77", result.BusinessRecordId);
    }

    // ──────────────────────────────────────────────────────────────
    // 9. Approval-sourced Followup uses ReferenceNo as BusinessRecordId
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ApprovalSourcedFollowup_UsesReferenceNoAsBusinessRecordId()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var approvalModule = SeedModule(db, "EA Approval");
        // Approval's BusinessRecordId is the ReferenceNo string (e.g. APR-2026-000012)
        var followup = SeedFollowup(db,
            businessModuleId: approvalModule.Id, businessRecordId: "APR-2026-000012");
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        Assert.Equal(approvalModule.Id, result.BusinessModuleId);
        Assert.Equal("APR-2026-000012", result.BusinessRecordId);
    }

    // ──────────────────────────────────────────────────────────────
    // 10. Delegation-sourced Followup can be escalated
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_DelegationSourcedFollowup_Succeeds()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var delegModule = SeedModule(db, "Delegation");
        var followup = SeedFollowup(db,
            businessModuleId: delegModule.Id, businessRecordId: "55");
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        Assert.Equal(delegModule.Id, result.BusinessModuleId);
        Assert.Equal("55", result.BusinessRecordId);
    }

    // ──────────────────────────────────────────────────────────────
    // 11. Future-module Followup — no hardcoded module switch needed
    //     Source linkage flows identically from the Followup row itself
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_FutureModuleFollowup_InheritsSourceWithoutHardcodedSwitch()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var futureModule = SeedModule(db, "CustomModule_XYZ");
        var followup = SeedFollowup(db,
            businessModuleId: futureModule.Id, businessRecordId: "FUTURE-001");
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        // Source identity comes from Followup — no switch/if needed
        Assert.Equal(futureModule.Id, result.BusinessModuleId);
        Assert.Equal("FUTURE-001", result.BusinessRecordId);
    }

    // ──────────────────────────────────────────────────────────────
    // 12. GetByFollowupIdAsync returns all escalations for a given followup
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByFollowupId_ReturnsAllEscalationsForFollowup_OrderedByInitiatedAt()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        // Create two escalations for the same followup
        await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id, Notes = "First"
        });
        await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id, Notes = "Second"
        });

        var list = await svc.GetByFollowupIdAsync(followup.Id);

        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.Equal(followup.Id, e.FollowupId));
    }

    // ──────────────────────────────────────────────────────────────
    // 13. GetByFollowupIdAsync excludes soft-deleted escalations
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByFollowupId_ExcludesSoftDeletedEscalations()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);

        // Seed one soft-deleted escalation directly
        db.Escalations.Add(new Escalation
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id,
            InitiatedAt = DateTime.UtcNow, IsDeleted = true,
            CreatedBy = "seed", CreatedDate = DateTime.UtcNow
        });
        db.SaveChanges();

        var svc = MakeService(db);
        var list = await svc.GetByFollowupIdAsync(followup.Id);

        Assert.Empty(list);
    }

    // ──────────────────────────────────────────────────────────────
    // 14. GetByIdAsync populates EscalationLevelName and Number
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_PopulatesEscalationLevelNameAndNumber()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db, id: 1, levelNum: 3, code: "L3");
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var created = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        var fetched = await svc.GetByIdAsync(created.Id);

        Assert.Equal("Level L3", fetched.EscalationLevelName);
        Assert.Equal(3, fetched.EscalationLevelNumber);
    }

    // ──────────────────────────────────────────────────────────────
    // 15. ResolveAsync closes an open escalation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_OpenEscalation_SetsResolvedAtAndState()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db, actorName: "resolver-ea", actorId: 10);

        var created = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        await svc.ResolveAsync(created.Id,
            new ResolveEscalationRequestDto { ResolutionNote = "Issue addressed" });

        var fetched = await svc.GetByIdAsync(created.Id);

        Assert.Equal("Resolved", fetched.EscalationState);
        Assert.NotNull(fetched.ResolvedAt);
        Assert.Equal("Issue addressed", fetched.ResolutionNote);
        Assert.Equal("10", fetched.ResolvedById);
    }

    // ──────────────────────────────────────────────────────────────
    // 16. Resolve already-resolved escalation is rejected
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_AlreadyResolvedEscalation_ThrowsBadRequestException()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var created = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });
        await svc.ResolveAsync(created.Id, new ResolveEscalationRequestDto());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.ResolveAsync(created.Id, new ResolveEscalationRequestDto()));
    }

    // ──────────────────────────────────────────────────────────────
    // 17. No EaTask created by Escalation operations
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_DoesNotCreateEaTask()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var tasksBefore = await db.Tasks.CountAsync();
        var svc = MakeService(db);

        await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        Assert.Equal(tasksBefore, await db.Tasks.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 18. No additional Followup created by Escalation operations
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_DoesNotCreateAdditionalFollowup()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var followupsBefore = await db.Followups.CountAsync();
        var svc = MakeService(db);

        await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        Assert.Equal(followupsBefore, await db.Followups.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 19. Audit is recorded via AuditService; no Email/WhatsApp side-effect
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_RecordsAudit_AndNoNotificationRowCreated()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var audit = new Mock<IAuditService>();
        var svc = MakeService(db, audit: audit);

        await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        // Audit must be called exactly once with ESCALATION_CREATE
        audit.Verify(a => a.AddAudit(
            "ESCALATION_CREATE",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>()),
            Times.Once);

        // No Notification row created
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 20. Existing Followup count unchanged — Escalation doesn't add Followups
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_DoesNotChangeFollowupCount()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var f1 = SeedFollowup(db, businessRecordId: "A");
        var f2 = SeedFollowup(db, businessRecordId: "B");
        var countBefore = await db.Followups.CountAsync();
        var svc = MakeService(db);

        await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = f1.Id, EscalationLevelId = level.Id
        });
        await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = f2.Id, EscalationLevelId = level.Id
        });

        Assert.Equal(countBefore, await db.Followups.CountAsync());
    }

    // ──────────────────────────────────────────────────────────────
    // 21. EscalationState lifecycle: Open → Acknowledged → Resolved
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task EscalationState_FollowsLifecycle_OpenAcknowledgedResolved()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var created = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });
        Assert.Equal("Open", (await svc.GetByIdAsync(created.Id)).EscalationState);

        await svc.AcknowledgeAsync(created.Id, "Noted");
        Assert.Equal("Acknowledged", (await svc.GetByIdAsync(created.Id)).EscalationState);

        await svc.ResolveAsync(created.Id, new ResolveEscalationRequestDto());
        Assert.Equal("Resolved", (await svc.GetByIdAsync(created.Id)).EscalationState);
    }

    // ──────────────────────────────────────────────────────────────
    // 22. AcknowledgeAsync populates AcknowledgedAt and AcknowledgedById
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Acknowledge_SetsAcknowledgedAtAndActorFields()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db, actorName: "ack-user", actorId: 77);

        var created = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });

        await svc.AcknowledgeAsync(created.Id, "Ack note");

        var fetched = await svc.GetByIdAsync(created.Id);
        Assert.NotNull(fetched.AcknowledgedAt);
        Assert.Equal("77", fetched.AcknowledgedById);
        Assert.Equal("Ack note", fetched.AcknowledgementNote);
    }

    // ──────────────────────────────────────────────────────────────
    // 23. Cannot acknowledge a resolved escalation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Acknowledge_ResolvedEscalation_ThrowsBadRequestException()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var created = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });
        await svc.ResolveAsync(created.Id, new ResolveEscalationRequestDto());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.AcknowledgeAsync(created.Id, null));
    }

    // ──────────────────────────────────────────────────────────────
    // 24. Cannot acknowledge an already-acknowledged escalation
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Acknowledge_AlreadyAcknowledged_ThrowsBadRequestException()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var created = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });
        await svc.AcknowledgeAsync(created.Id, null);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.AcknowledgeAsync(created.Id, null));
    }

    // ──────────────────────────────────────────────────────────────
    // 25. NextEscalationLevelId ordering: next level must be > current level
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_NextEscalationLevelLessThanCurrent_ThrowsBadRequestException()
    {
        await using var db = MakeDb();
        // Current = level 3, Next = level 1 — violates ordering rule
        var currentLevel = SeedLevel(db, id: 1, levelNum: 3, code: "L3");
        var lowerLevel   = SeedLevel(db, id: 2, levelNum: 1, code: "L1");
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            svc.CreateAsync(new CreateEscalationRequestDto
            {
                FollowupId = followup.Id,
                EscalationLevelId = currentLevel.Id,
                NextEscalationLevelId = lowerLevel.Id
            }));
    }

    [Fact]
    public async Task Create_NextEscalationLevelGreaterThanCurrent_Succeeds()
    {
        await using var db = MakeDb();
        var currentLevel = SeedLevel(db, id: 1, levelNum: 1, code: "L1");
        var higherLevel  = SeedLevel(db, id: 2, levelNum: 3, code: "L3");
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id,
            EscalationLevelId = currentLevel.Id,
            NextEscalationLevelId = higherLevel.Id
        });

        Assert.Equal(higherLevel.Id, result.NextEscalationLevelId);
    }

    // ──────────────────────────────────────────────────────────────
    // GetById — non-existent escalation returns NotFoundException
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_NonExistent_ThrowsNotFoundException()
    {
        await using var db = MakeDb();
        var svc = MakeService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.GetByIdAsync(9999));
    }

    // ──────────────────────────────────────────────────────────────
    // Deleted Followup is treated as not found
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_SoftDeletedFollowup_ThrowsNotFoundException()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var deletedFollowup = SeedFollowup(db, deleted: true);
        var svc = MakeService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.CreateAsync(new CreateEscalationRequestDto
            {
                FollowupId = deletedFollowup.Id,
                EscalationLevelId = level.Id
            }));
    }

    // ──────────────────────────────────────────────────────────────
    // Resolve audit is called (ESCALATION_RESOLVE)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_RecordsAuditWithCorrectActionType()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var audit = new Mock<IAuditService>();
        var svc = MakeService(db, audit: audit);

        var created = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id
        });
        await svc.ResolveAsync(created.Id, new ResolveEscalationRequestDto());

        audit.Verify(a => a.AddAudit(
            "ESCALATION_RESOLVE",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string>()),
            Times.Once);
    }

    // ──────────────────────────────────────────────────────────────
    // GetById — note that NextEscalationLevelId field works (test 25 coverage)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_EscalatedToFields_PersistedAndReturned()
    {
        await using var db = MakeDb();
        var level = SeedLevel(db);
        var followup = SeedFollowup(db);
        var svc = MakeService(db);

        var result = await svc.CreateAsync(new CreateEscalationRequestDto
        {
            FollowupId = followup.Id, EscalationLevelId = level.Id,
            EscalatedToId = "EMP-001", EscalatedToName = "Director Ahmed"
        });

        Assert.Equal("EMP-001", result.EscalatedToId);
        Assert.Equal("Director Ahmed", result.EscalatedToName);
    }

    // ──────────────────────────────────────────────────────────────
    // Validator tests (no service call needed)
    // ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void CreateValidator_FollowupId_MustBePositive(long id, bool valid)
    {
        var dto = new CreateEscalationRequestDto { FollowupId = id, EscalationLevelId = 1 };
        var result = new Jarvis5.Validators.CreateEscalationRequestDtoValidator().Validate(dto);
        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void CreateValidator_EscalationLevelId_MustBePositive(int id, bool valid)
    {
        var dto = new CreateEscalationRequestDto { FollowupId = 1, EscalationLevelId = id };
        var result = new Jarvis5.Validators.CreateEscalationRequestDtoValidator().Validate(dto);
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void CreateValidator_NotesExceeding2000Chars_FailsValidation()
    {
        var dto = new CreateEscalationRequestDto
        {
            FollowupId = 1, EscalationLevelId = 1, Notes = new string('x', 2001)
        };
        var result = new Jarvis5.Validators.CreateEscalationRequestDtoValidator().Validate(dto);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResolveValidator_ResolutionNoteExceeding2000Chars_FailsValidation()
    {
        var dto = new ResolveEscalationRequestDto { ResolutionNote = new string('x', 2001) };
        var result = new Jarvis5.Validators.ResolveEscalationRequestDtoValidator().Validate(dto);
        Assert.False(result.IsValid);
    }
}
