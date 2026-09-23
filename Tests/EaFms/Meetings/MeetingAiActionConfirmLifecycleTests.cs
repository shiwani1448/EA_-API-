using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Jarvis5.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// Confirm's interaction with the REAL Meeting -> Delegation lifecycle (Step 5B-3) needs
/// real Postgres transactions/row locks, same limitation already documented in
/// MeetingToDelegationTests — reuses that file's connection/seeding pattern. Proves:
/// (a) AI-confirmed MeetingActions become Delegations exactly like manual ones, but only
/// at the same lifecycle point (Meeting completion) that already existed; (b) confirming
/// after completion does not retroactively create Delegations — current architecture,
/// not something this phase changes; (c) a genuine Postgres constraint failure mid-batch
/// rolls back the whole confirmation.
/// </summary>
public class MeetingAiActionConfirmLifecycleTests
{
    private const string ConnectionString = "Host=localhost;Port=5432;Database=DB_Studio5Jarvis;Username=postgres;Password=123456";

    private static EaFmsDbContext MakeRealDb() =>
        new(new DbContextOptionsBuilder<EaFmsDbContext>().UseNpgsql(ConnectionString).Options);

    private static async Task<long> ResolveModuleIdAsync(EaFmsDbContext db, string name) =>
        await db.BusinessModules.Where(m => m.Name == name && m.IsActive && !m.IsDeleted).Select(m => m.Id).SingleAsync();

    private static async Task<int> ResolveInProgressStatusIdAsync(EaFmsDbContext db) =>
        await db.Statuses.Where(s => s.Name == "In Progress").Select(s => s.Id).FirstAsync();

    private static IServiceProvider MakeServiceProvider(IClaudeClient claude)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude);
        return sp.Object;
    }

    private static MeetingAiService MakeAiService(EaFmsDbContext db) => new(
        db,
        MakeServiceProvider(Mock.Of<IClaudeClient>()),
        Mock.Of<IMeetingActionExtractionPromptBuilder>(),
        Mock.Of<hrms_api.Services.IDocumentExtractionService>(),
        Mock.Of<IMeetingCompletionFileStore>(),
        NullLogger<MeetingAiService>.Instance,
        Options.Create(new ClaudeOptions()));

    private static DelegationService MakeRealDelegationService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit)
    {
        var eaTasks = new EaTaskService(db, new EaTaskRepository(db), new TatRuleRepository(db),
            new CreateEaTaskDtoValidator(), user, audit);
        return new DelegationService(db, user, audit, new DelegationRepository(db), eaTasks,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(),
            new TaskReviewService(db, new TaskReviewRepository(db), user, audit));
    }

    private static async Task<(Meeting meeting, long meetingModuleId)> SeedInProgressMeetingAsync(EaFmsDbContext db, string marker, string actor = "ai-confirm-test")
    {
        var meetingModuleId = await ResolveModuleIdAsync(db, "Meeting");
        var inProgressStatusId = await ResolveInProgressStatusIdAsync(db);
        var now = DateTime.UtcNow;

        var meeting = new Meeting
        {
            Title = $"AI confirm lifecycle test meeting {marker} (disposable test)",
            MeetingNumber = $"MTG-AICONFIRM-{marker}",
            DoerIds = Array.Empty<string>(), DoerNames = Array.Empty<string>(),
            CreatedBy = actor, CreatedDate = now, IsDeleted = false
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var workflow = new WorkflowInstance
        {
            BusinessModuleId = meetingModuleId,
            BusinessRecordId = meeting.Id.ToString(),
            StatusId = inProgressStatusId,
            StartedAt = now.AddMinutes(-10),
            TatStartedAt = now.AddMinutes(-10),
            IsActive = true,
            CreatedBy = actor, CreatedDate = now
        };
        db.WorkflowInstances.Add(workflow);
        await db.SaveChangesAsync();

        meeting.WorkflowInstanceId = workflow.Id;
        await db.SaveChangesAsync();

        db.Tasks.Add(new EaTask
        {
            BusinessModuleId = meetingModuleId, ModuleName = "Meeting", BusinessRecordId = meeting.Id.ToString(),
            Task = meeting.Title!, ExecutionStatus = "InProgress", StartedAt = workflow.TatStartedAt,
            WorkflowInstanceId = workflow.Id, IsActive = true, CreatedBy = actor, CreatedDate = now
        });
        await db.SaveChangesAsync();

        return (meeting, meetingModuleId);
    }

    private static Mock<IMeetingCompletionFileStore> MakeFileStoreMock()
    {
        var files = new Mock<IMeetingCompletionFileStore>();
        files.Setup(f => f.ValidateAsync(It.IsAny<IFormFile>(), It.IsAny<CancellationToken>())).ReturnsAsync(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        files.Setup(f => f.SaveAsync(It.IsAny<long>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>())).ReturnsAsync("Content/MeetingCompletion/disposable-ai-confirm-test.pdf");
        return files;
    }

    private static MeetingLifecycleService MakeLifecycleService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit, DelegationService delegations)
    {
        var execution = new Mock<IWorkflowExecutionService>();
        execution.Setup(x => x.CompleteAsync(It.IsAny<long>(), It.IsAny<CompleteWorkRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompleteWorkResponseDto { Workflow = new WorkflowResponseDto() });
        var meetings = new Mock<IMeetingService>();
        meetings.Setup(x => x.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeetingDetailResponseDto { TatSummary = new MeetingTatSummaryDto() });
        return new MeetingLifecycleService(db, execution.Object, user, audit, meetings.Object, MakeFileStoreMock().Object, delegations, NullLogger<MeetingLifecycleService>.Instance);
    }

    private static IFormFile MakeFakePdf() => Mock.Of<IFormFile>(f => f.FileName == "test.pdf");

    // (a) Confirm BEFORE completion, then Complete -> existing Delegation conversion picks
    // up the AI-confirmed actions exactly like manually created ones.
    [Fact]
    public async Task ConfirmBeforeCompletion_ThenComplete_ConvertsAiConfirmedActionsToDelegations_ExactlyLikeManual()
    {
        await using var db = MakeRealDb();
        var marker = $"before-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);

        var confirmResult = await MakeAiService(db).ConfirmActionsAsync(meeting.Id, new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = $"AI action {marker}", DoerId = $"EMP-AI-{marker}", DoerName = "AI Confirmed Doer", Priority = "High" } }
        }, "ea-actor");
        var actionId = confirmResult.CreatedActions.Single().Id;

        // Zero Delegations exist yet — confirm alone never creates one.
        Assert.Equal(0, await db.Delegations.CountAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == actionId.ToString()));

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations);
        await lifecycle.CompleteAsync(meeting.Id, new MeetingCompleteRequestDto { CompletionMom = "Done", CompletionPdf = MakeFakePdf() }, default);

        await using var verify = MakeRealDb();
        var delegation = await verify.Delegations.SingleAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == actionId.ToString());
        Assert.Equal($"AI action {marker}", delegation.Title);
        Assert.Equal($"EMP-AI-{marker}", delegation.DoerId);
        Assert.Equal("AI Confirmed Doer", delegation.DoerNameSnapshot);
    }

    // (b) Confirm AFTER completion -> no Delegation is retroactively created. Current
    // architecture behavior (Delegation conversion only happens inside CompleteAsync,
    // which already ran) — not changed or "fixed" by this phase.
    [Fact]
    public async Task ConfirmAfterCompletion_CreatesNoDelegation_CurrentArchitectureBehavior()
    {
        await using var db = MakeRealDb();
        var marker = $"after-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations);
        await lifecycle.CompleteAsync(meeting.Id, new MeetingCompleteRequestDto { CompletionMom = "Done", CompletionPdf = MakeFakePdf() }, default);

        var confirmResult = await MakeAiService(db).ConfirmActionsAsync(meeting.Id, new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = $"Post-completion AI action {marker}", DoerId = $"EMP-POST-{marker}" } }
        }, "ea-actor");
        var actionId = confirmResult.CreatedActions.Single().Id;

        await using var verify = MakeRealDb();
        Assert.True(await verify.MeetingActions.AnyAsync(a => a.Id == actionId)); // the MeetingAction itself IS created
        Assert.Equal(0, await verify.Delegations.CountAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == actionId.ToString())); // but no Delegation
        var reloadedMeeting = await verify.Meetings.SingleAsync(m => m.Id == meeting.Id);
        Assert.NotNull(reloadedMeeting.CompletedAt); // meeting stays completed, confirm didn't touch it
    }

    // (c) Real Postgres constraint failure mid-batch rolls back the whole confirmation —
    // FluentValidation would normally reject a >500-char Title at the API layer; this test
    // calls the service directly (as a compromised/older client bypassing validation would)
    // to prove the database-level safety net still holds for the whole transaction.
    [Fact]
    public async Task Confirm_TransactionRollsBackEntireBatch_OnRealColumnConstraintViolation()
    {
        await using var db = MakeRealDb();
        var marker = $"rollback-{Guid.NewGuid():N}";
        var (meeting, _) = await SeedInProgressMeetingAsync(db, marker);

        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions =
            {
                new CreateMeetingActionDto { Title = $"Valid first action {marker}", DoerId = "EMP-VALID" },
                new CreateMeetingActionDto { Title = new string('x', 501) }, // exceeds ea_meeting_actions."Title" varchar(500)
            },
        };

        await Assert.ThrowsAnyAsync<Exception>(() => MakeAiService(db).ConfirmActionsAsync(meeting.Id, request, "ea-actor"));

        await using var verify = MakeRealDb();
        Assert.Equal(0, await verify.MeetingActions.CountAsync(a => a.MeetingId == meeting.Id));
    }

    // Manual + AI-confirmed actions on the same Meeting are treated identically at
    // completion (both convert to Delegations together, no special-casing by origin).
    [Fact]
    public async Task ManualAndAiConfirmedActions_OnSameMeeting_BothConvertToDelegations()
    {
        await using var db = MakeRealDb();
        var marker = $"mixed-{Guid.NewGuid():N}";
        var (meeting, meetingModuleId) = await SeedInProgressMeetingAsync(db, marker);

        // Manual action, created the existing way (direct entity insert, same as the
        // manual controller's own persistence shape).
        var manual = new MeetingAction
        {
            MeetingId = meeting.Id, Title = $"Manual action {marker}", DoerId = $"EMP-MANUAL-{marker}",
            CreatedBy = "ea-actor", CreatedDate = DateTime.UtcNow,
        };
        db.MeetingActions.Add(manual);
        await db.SaveChangesAsync();

        var confirmResult = await MakeAiService(db).ConfirmActionsAsync(meeting.Id, new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = $"AI action {marker}", DoerId = $"EMP-AI-{marker}" } }
        }, "ea-actor");
        var aiActionId = confirmResult.CreatedActions.Single().Id;

        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 1);
        var audit = new AuditService(db, user);
        var delegations = MakeRealDelegationService(db, user, audit);
        var lifecycle = MakeLifecycleService(db, user, audit, delegations);
        await lifecycle.CompleteAsync(meeting.Id, new MeetingCompleteRequestDto { CompletionMom = "Done", CompletionPdf = MakeFakePdf() }, default);

        await using var verify = MakeRealDb();
        Assert.Equal(1, await verify.Delegations.CountAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == manual.Id.ToString()));
        Assert.Equal(1, await verify.Delegations.CountAsync(d => d.SourceBusinessModuleId == meetingModuleId && d.SourceEntityId == aiActionId.ToString()));
    }
}
