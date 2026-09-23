using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Meetings;

/// <summary>
/// Phase 2: POST /api/ea/meetings/{meetingId}/ai/actions/confirm. Runs against an
/// in-memory EaFmsDbContext with IClaudeClient etc. mocked. Business-logic-only checks —
/// the real Postgres-backed Delegation/EaTask lifecycle and transaction-rollback proofs
/// live in MeetingAiActionConfirmLifecycleTests.
/// </summary>
public class MeetingAiActionConfirmServiceTests
{
    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static long SeedMeeting(EaFmsDbContext db, string title = "AI confirm test meeting")
    {
        var meeting = new Meeting
        {
            Title = title,
            DoerIds = Array.Empty<string>(),
            DoerNames = Array.Empty<string>(),
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow,
        };
        db.Meetings.Add(meeting);
        db.SaveChanges();
        return meeting.Id;
    }

    // MeetingAiService resolves IClaudeClient lazily via IServiceProvider (never as a direct
    // constructor dependency) precisely so that ConfirmActionsAsync works even when no
    // Claude client/key is configured at all — this stub provider mirrors that at test time.
    private static IServiceProvider MakeServiceProvider(IClaudeClient claude)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude);
        return sp.Object;
    }

    private static MeetingAiService Service(EaFmsDbContext db, Mock<IClaudeClient>? claude = null) => new(
        db,
        MakeServiceProvider((claude ?? new Mock<IClaudeClient>()).Object),
        Mock.Of<IMeetingActionExtractionPromptBuilder>(),
        Mock.Of<hrms_api.Services.IDocumentExtractionService>(),
        Mock.Of<IMeetingCompletionFileStore>(),
        NullLogger<MeetingAiService>.Instance,
        Options.Create(new ClaudeOptions()));

    // 2. confirm one AI-proposed action
    [Fact]
    public async Task ConfirmOneAction_CreatesOneMeetingAction_LinkedToTheMeeting()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "Send proposal", DoerName = "Rahul" } },
        };

        var result = await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        Assert.Equal(meetingId, result.MeetingId);
        var created = Assert.Single(result.CreatedActions);
        Assert.Equal("Send proposal", created.Title);
        Assert.Equal("Rahul", created.DoerName);

        var saved = await db.MeetingActions.SingleAsync();
        Assert.Equal(meetingId, saved.MeetingId); // 8. correct Meeting link
        Assert.Equal("ea-actor", saved.CreatedBy);
    }

    // 3. confirm multiple actions
    [Fact]
    public async Task ConfirmMultipleActions_CreatesOneMeetingActionEach()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions =
            {
                new CreateMeetingActionDto { Title = "Action A" },
                new CreateMeetingActionDto { Title = "Action B" },
                new CreateMeetingActionDto { Title = "Action C" },
            },
        };

        var result = await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        Assert.Equal(3, result.CreatedActions.Count);
        Assert.Equal(3, await db.MeetingActions.CountAsync());
        Assert.True(await db.MeetingActions.AllAsync(a => a.MeetingId == meetingId));
    }

    // 4. EA-edited values are persisted, not any hypothetical original AI suggestion
    [Fact]
    public async Task EaEditedValues_ArePersisted_ConfirmNeverReadsAiOutput()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        // Confirm has no access to the original AI suggestion at all — only what is
        // submitted here is ever written, exactly like AI:{title:"Send revised proposal",
        // doerName:"Rahul", priority:"High"} edited by the EA to the values below.
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions =
            {
                new CreateMeetingActionDto
                {
                    Title = "Send final revised proposal",
                    DoerId = "EMP014",
                    DoerName = "Rahul Sharma",
                    Priority = "Normal",
                },
            },
        };

        await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        var saved = await db.MeetingActions.SingleAsync();
        Assert.Equal("Send final revised proposal", saved.Title);
        Assert.Equal("EMP014", saved.DoerId);
        Assert.Equal("Rahul Sharma", saved.DoerName);
        Assert.Equal("Normal", saved.Priority);
    }

    // 5. doerId supplied by frontend persists correctly (trimmed, never invented)
    [Fact]
    public async Task DoerId_PersistsTrimmed_AndIsNeverInvented()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions =
            {
                new CreateMeetingActionDto { Title = "With doer", DoerId = "  EMP014  " },
                new CreateMeetingActionDto { Title = "Without doer", DoerName = "Name only, no id" },
            },
        };

        await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        var saved = await db.MeetingActions.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal("EMP014", saved[0].DoerId);
        Assert.Null(saved[1].DoerId); // never derived from DoerName
        Assert.Equal("Name only, no id", saved[1].DoerName);
    }

    // 6. doerName snapshot persists correctly
    [Fact]
    public async Task DoerName_PersistsVerbatim()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "X", DoerName = "  Rahul Sharma  " } },
        };

        await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        // DoerName is stored verbatim (not trimmed) — same as the manual create endpoint.
        Assert.Equal("  Rahul Sharma  ", (await db.MeetingActions.SingleAsync()).DoerName);
    }

    // 7. null optional fields remain supported (existing contract: everything but Title's
    // usefulness is optional; the entity itself has no required fields beyond CreatedBy)
    [Fact]
    public async Task NullOptionalFields_AreSupported()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto() }, // everything null
        };

        var result = await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        Assert.Single(result.CreatedActions);
        var saved = await db.MeetingActions.SingleAsync();
        Assert.Null(saved.Title);
        Assert.Null(saved.Description);
        Assert.Null(saved.DoerId);
        Assert.Null(saved.DoerName);
        Assert.Null(saved.Priority);
        Assert.Null(saved.DueDate);
        Assert.Null(saved.Status);
    }

    // 11. no AI-specific Delegation path: confirm creates zero Delegations/EaTasks directly
    [Fact]
    public async Task Confirm_CreatesNoDelegation_AndNoEaTask_Directly()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "X", DoerId = "EMP1" } },
        };

        await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        Assert.Equal(0, await db.Delegations.CountAsync());
        Assert.Equal(0, await db.Tasks.CountAsync());
    }

    // 12. no Claude call occurs during confirm
    [Fact]
    public async Task Confirm_NeverCallsClaude()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        var claude = new Mock<IClaudeClient>();
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "X" } },
        };

        await Service(db, claude).ConfirmActionsAsync(meetingId, request, "ea-actor");

        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 13, 14. confirm does not modify Meeting TAT or lifecycle
    [Fact]
    public async Task Confirm_DoesNotModify_MeetingLifecycleOrTat()
    {
        await using var db = NewDb();
        var meeting = new Meeting
        {
            Title = "Lifecycle check", DoerIds = Array.Empty<string>(), DoerNames = Array.Empty<string>(),
            StatusId = 7, CreatedBy = "seed", CreatedDate = DateTime.UtcNow,
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        var meetingId = meeting.Id;
        var before = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meetingId);

        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "X" } },
        };
        await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        var after = await db.Meetings.AsNoTracking().SingleAsync(m => m.Id == meetingId);
        Assert.Equal(before.StatusId, after.StatusId);
        Assert.Equal(before.CompletedAt, after.CompletedAt);
        Assert.Equal(before.WorkflowInstanceId, after.WorkflowInstanceId);
        Assert.Equal(before.CompletionMom, after.CompletionMom);
        Assert.Equal(0, await db.WorkflowInstances.CountAsync());
        Assert.Equal(0, await db.WorkPauses.CountAsync());
    }

    // 15. unknown Meeting returns correct error
    [Fact]
    public async Task UnknownMeeting_ThrowsNotFound()
    {
        await using var db = NewDb();
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "X" } },
        };

        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).ConfirmActionsAsync(999, request, "ea-actor"));
        Assert.Equal(0, await db.MeetingActions.CountAsync());
    }

    // Zero actions submitted is valid and creates nothing (no invented "must have 1" rule).
    [Fact]
    public async Task ZeroActions_IsValid_CreatesNothing()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);

        var result = await Service(db).ConfirmActionsAsync(meetingId, new ConfirmMeetingAiActionsRequestDto(), "ea-actor");

        Assert.Empty(result.CreatedActions);
        Assert.Equal(0, await db.MeetingActions.CountAsync());
    }

    // Repeated identical submission: no idempotency mechanism exists anywhere in the
    // existing MeetingAction architecture (the manual endpoint has none either), so
    // confirming twice creates two separate rows — documented current behavior, not a bug
    // introduced by this phase.
    [Fact]
    public async Task RepeatedIdenticalSubmission_CreatesDuplicateMeetingActions_NoIdempotencyExists()
    {
        await using var db = NewDb();
        var meetingId = SeedMeeting(db);
        var request = new ConfirmMeetingAiActionsRequestDto
        {
            Actions = { new CreateMeetingActionDto { Title = "Same action", DoerId = "EMP1" } },
        };

        await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");
        await Service(db).ConfirmActionsAsync(meetingId, request, "ea-actor");

        Assert.Equal(2, await db.MeetingActions.CountAsync(a => a.Title == "Same action"));
    }

    // Request/response shape: reuses the existing CreateMeetingActionDto/MeetingActionDto,
    // no parallel AI-specific action shape was invented.
    [Fact]
    public void RequestAndResponse_ReuseExistingMeetingActionDtos()
    {
        Assert.Equal(typeof(List<CreateMeetingActionDto>), typeof(ConfirmMeetingAiActionsRequestDto).GetProperty(nameof(ConfirmMeetingAiActionsRequestDto.Actions))!.PropertyType);
        Assert.Equal(typeof(List<MeetingActionDto>), typeof(MeetingAiActionsConfirmResponseDto).GetProperty(nameof(MeetingAiActionsConfirmResponseDto.CreatedActions))!.PropertyType);
    }
}
