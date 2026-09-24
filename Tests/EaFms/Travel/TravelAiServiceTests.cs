using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services;
using Jarvis5.Services.Ai;
using Jarvis5.Services.EaFms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Jarvis5.Tests.EaFms.Travel;

/// <summary>
/// Travel AI (options suggest/confirm, itinerary draft, checklist draft). ConfirmOptions
/// runs against the REAL TravelBookingService (not mocked) so these tests prove genuine
/// reuse of the existing booking-creation path, including its own business-state
/// precondition (EnsureReady) — never a parallel/duplicated rule. IClaudeClient is
/// mocked — no real Anthropic API call is ever made from this test suite.
/// </summary>
public class TravelAiServiceTests
{
    private static EaFmsDbContext NewDb() => new(new DbContextOptionsBuilder<EaFmsDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static TravelRequest SeedTravelRequest(
        EaFmsDbContext db, long id,
        string businessState = "Upcoming", string approvalState = "NotRequired",
        string? from = "Mumbai", string? to = "Delhi")
    {
        var entity = new TravelRequest
        {
            Id = id,
            ReferenceNo = $"TRV-AI-TEST-{id:D6}",
            EaTaskId = 5000 + id,
            BusinessState = businessState,
            ApprovalState = approvalState,
            ApprovalRequired = false,
            CurrentCycleNo = 0,
            FromLocation = from,
            ToLocation = to,
            DepartureDate = DateTime.UtcNow.AddDays(10),
            ReturnDate = DateTime.UtcNow.AddDays(15),
            CreatedBy = "seed",
            CreatedDate = DateTime.UtcNow,
        };
        db.TravelRequests.Add(entity);
        db.SaveChanges();
        return entity;
    }

    private static (Mock<IClaudeClient> Claude, Mock<ITravelAiPromptBuilder> Prompts) Mocks()
    {
        var claude = new Mock<IClaudeClient>();
        var prompts = new Mock<ITravelAiPromptBuilder>();
        prompts.Setup(p => p.BuildOptionsSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildOptionsUserPrompt(It.IsAny<TravelRequest>())).Returns("user");
        prompts.Setup(p => p.BuildItinerarySystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildItineraryUserPrompt(It.IsAny<TravelRequest>(), It.IsAny<List<TravelBookingResponseDto>>())).Returns("user");
        prompts.Setup(p => p.BuildChecklistSystemPrompt()).Returns("system");
        prompts.Setup(p => p.BuildChecklistUserPrompt(It.IsAny<TravelRequest>())).Returns("user");
        return (claude, prompts);
    }

    private static IServiceProvider MakeServiceProvider(IClaudeClient claude)
    {
        var sp = new Mock<IServiceProvider>();
        sp.Setup(s => s.GetService(typeof(IClaudeClient))).Returns(claude);
        return sp.Object;
    }

    private static TravelAiService Service(
        EaFmsDbContext db, Mock<IClaudeClient> claude, Mock<ITravelAiPromptBuilder> prompts, int maxRetries = 2)
    {
        var user = Mock.Of<ICurrentUserService>(u => u.UserName == "ea-actor" && u.UserId == 1L);
        var bookings = new TravelBookingService(db, user, Mock.Of<IAuditService>());
        return new TravelAiService(
            db, MakeServiceProvider(claude.Object), prompts.Object, bookings,
            NullLogger<TravelAiService>.Instance,
            Options.Create(new ClaudeOptions { MaxRetries = maxRetries }));
    }

    private const string ValidOptionsJson = """{"proposedOptions":[{"bookingType":"Flight","provider":"Generic Airline","estimatedCost":15000,"currency":"INR","details":"Mumbai to Delhi, economy","notes":"Illustrative only"}]}""";

    // 1. Suggest options with route/dates present
    [Fact]
    public async Task SuggestOptions_WithRouteAndDates_ReturnsOptions_AndAlwaysWarns()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 1);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidOptionsJson);

        var result = await Service(db, claude, prompts).SuggestOptionsAsync(1);

        var option = Assert.Single(result.ProposedOptions);
        Assert.Equal("Flight", option.BookingType);
        Assert.NotNull(result.WarningMessage);
        Assert.Contains("illustrative", result.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    // Full audit trail: every generated suggestion is logged verbatim into its own
    // per-module table, mirroring SCIH's separate SCIH_Analysis/SCIH_SolutionDesign tables
    // — see TravelOptionSuggestion's own doc comment.
    [Fact]
    public async Task SuggestOptions_WritesTravelOptionSuggestionRow()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 30);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidOptionsJson);

        await Service(db, claude, prompts).SuggestOptionsAsync(30);

        var log = Assert.Single(db.TravelOptionSuggestions);
        Assert.Equal(30, log.TravelRequestId);
        Assert.False(log.IsApplied);
        Assert.Contains("Flight", log.ProposedOptionsJson);
    }

    // 2. No input at all -> business rule error, no Claude call
    [Fact]
    public async Task SuggestOptions_NoRouteDatesOrHotel_ThrowsBusinessRuleException()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 2, from: null, to: null);
        db.TravelRequests.Single(t => t.Id == 2).DepartureDate = null;
        await db.SaveChangesAsync();
        var (claude, prompts) = Mocks();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts).SuggestOptionsAsync(2));
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 3. Unknown travel request
    [Fact]
    public async Task SuggestOptions_UnknownTravelRequest_ThrowsNotFound()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db, claude, prompts).SuggestOptionsAsync(999));
    }

    // 4. Malformed JSON retries then succeeds
    [Fact]
    public async Task SuggestOptions_MalformedJson_RetriesThenSucceeds()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 3);
        var (claude, prompts) = Mocks();
        claude.SetupSequence(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not json")
            .ReturnsAsync(ValidOptionsJson);

        var result = await Service(db, claude, prompts, maxRetries: 2).SuggestOptionsAsync(3);

        Assert.Single(result.ProposedOptions);
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // 5. Malformed JSON exhausts retries
    [Fact]
    public async Task SuggestOptions_MalformedJson_ExhaustsRetries_Throws()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 4);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("still not json");

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts, maxRetries: 2).SuggestOptionsAsync(4));
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // Compare: needs at least 2 options
    [Fact]
    public async Task CompareOptions_FewerThanTwoOptions_ThrowsBusinessRuleException()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 20);
        var (claude, prompts) = Mocks();
        var request = new TravelAiCompareOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight" } },
        };

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts).CompareOptionsAsync(20, request));
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Compare: echoes submitted prices/providers back UNCHANGED, adds AI commentary,
    // marks exactly the recommended one
    [Fact]
    public async Task CompareOptions_EchoesSubmittedValuesUnchanged_AndMarksRecommendation()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 21);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"comparedOptions":[{"aiComment":"Cheapest, good timing.","recommended":true},{"aiComment":"More expensive, no advantage.","recommended":false}]}""");
        var request = new TravelAiCompareOptionsRequestDto
        {
            Options =
            {
                new CreateTravelBookingDto { BookingType = "Flight", Provider = "IndiGo", Cost = 13450m, Currency = "INR", DepartureDetails = "09:00-11:10" },
                new CreateTravelBookingDto { BookingType = "Flight", Provider = "Air India", Cost = 15800m, Currency = "INR", DepartureDetails = "14:00-16:20" },
            },
        };

        var result = await Service(db, claude, prompts).CompareOptionsAsync(21, request);

        Assert.Equal(2, result.ComparedOptions.Count);
        Assert.Equal("IndiGo", result.ComparedOptions[0].Provider);
        Assert.Equal(13450m, result.ComparedOptions[0].Cost); // unchanged from what was submitted
        Assert.Equal("Cheapest, good timing.", result.ComparedOptions[0].AiComment);
        Assert.True(result.ComparedOptions[0].IsRecommended);
        Assert.Equal("Air India", result.ComparedOptions[1].Provider);
        Assert.Equal(15800m, result.ComparedOptions[1].Cost);
        Assert.False(result.ComparedOptions[1].IsRecommended);
    }

    // Compare: mismatched result count is treated as a clear error, not a crash
    [Fact]
    public async Task CompareOptions_AiReturnsWrongCount_ThrowsBusinessRuleException()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 22);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"comparedOptions":[{"aiComment":"Only one.","recommended":true}]}""");
        var request = new TravelAiCompareOptionsRequestDto
        {
            Options =
            {
                new CreateTravelBookingDto { BookingType = "Flight" },
                new CreateTravelBookingDto { BookingType = "Hotel" },
            },
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts).CompareOptionsAsync(22, request));
        Assert.Contains("exactly one result per submitted option", ex.Message);
    }

    // Compare: never writes to the database, no matter the outcome
    [Fact]
    public async Task CompareOptions_NeverWritesToTheDatabase()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 23);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"comparedOptions":[{"aiComment":"A","recommended":true},{"aiComment":"B","recommended":false}]}""");
        var request = new TravelAiCompareOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight" }, new CreateTravelBookingDto { BookingType = "Hotel" } },
        };

        await Service(db, claude, prompts).CompareOptionsAsync(23, request);

        Assert.Equal(0, await db.TravelBookings.CountAsync());
    }

    // Compare: unknown travel request
    [Fact]
    public async Task CompareOptions_UnknownTravelRequest_ThrowsNotFound()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        var request = new TravelAiCompareOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight" }, new CreateTravelBookingDto { BookingType = "Hotel" } },
        };
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db, claude, prompts).CompareOptionsAsync(999999, request));
    }

    // 6. Confirm creates a REAL TravelBooking via the real TravelBookingService
    [Fact]
    public async Task ConfirmOptions_OnUpcomingRequest_CreatesRealBooking_ViaExistingService()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 5, businessState: "Upcoming", approvalState: "NotRequired");
        var (claude, prompts) = Mocks();
        var request = new ConfirmTravelAiOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight", Provider = "Air India", Cost = 12000m, Currency = "INR" } },
        };

        var result = await Service(db, claude, prompts).ConfirmOptionsAsync(5, request);

        var created = Assert.Single(result.CreatedBookings);
        Assert.Equal("Flight", created.BookingType);
        Assert.Equal("Air India", created.Provider);
        var saved = await db.TravelBookings.SingleAsync();
        Assert.Equal(5, saved.TravelRequestId);
        Assert.Equal(Jarvis5.Common.EaFms.TravelBookingRules.Requested, saved.BookingStatus); // starts Requested, same as manual create
        claude.Verify(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never); // 8. never calls Claude
    }

    // Confirming links back to whichever suggestion preceded it (Suggest or Compare,
    // whichever is more recent), marking that row IsApplied.
    [Fact]
    public async Task ConfirmOptions_AfterSuggest_MarksTheSuggestionApplied()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 40, businessState: "Upcoming", approvalState: "NotRequired");
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidOptionsJson);
        await Service(db, claude, prompts).SuggestOptionsAsync(40);
        var request = new ConfirmTravelAiOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight", Provider = "Air India", Cost = 12000m, Currency = "INR" } },
        };

        var confirmResult = await Service(db, claude, prompts).ConfirmOptionsAsync(40, request);

        var suggestion = await db.TravelOptionSuggestions.SingleAsync();
        Assert.True(suggestion.IsApplied);
        Assert.NotNull(suggestion.AppliedAt);
        var createdBookingId = confirmResult.CreatedBookings.Single().BookingId;
        Assert.Contains(createdBookingId.ToString(), suggestion.AppliedBookingIdsJson);
    }

    // 7. Confirm on a request that is NOT ready surfaces the SAME existing business rule
    [Fact]
    public async Task ConfirmOptions_OnDraftRequest_SurfacesExistingEnsureReadyRule_NoBypass()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 6, businessState: "Draft", approvalState: "NotRequired");
        var (claude, prompts) = Mocks();
        var request = new ConfirmTravelAiOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight" } },
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts).ConfirmOptionsAsync(6, request));
        Assert.Contains("not ready for booking execution", ex.Message);
        Assert.Equal(0, await db.TravelBookings.CountAsync());
    }

    // Confirm is also correctly rejected once the trip is Active (booking creation only
    // works during the "Upcoming" window, between approval and Start) — closes the loop on
    // the corrected lifecycle understanding: Suggest belongs at Draft, Compare/Confirm only
    // work in the Upcoming window, and are blocked again once Active.
    [Fact]
    public async Task ConfirmOptions_OnActiveRequest_Rejected_SameRuleAsDraft()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 24, businessState: "Active", approvalState: "NotRequired");
        var (claude, prompts) = Mocks();
        var request = new ConfirmTravelAiOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight" } },
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, claude, prompts).ConfirmOptionsAsync(24, request));
        Assert.Contains("not ready for booking execution", ex.Message);
        Assert.Equal(0, await db.TravelBookings.CountAsync());
    }

    // CanCreateBooking reflects the real state across the whole lifecycle, so the frontend
    // never has to re-derive TravelBookingService's own EnsureReady rule itself.
    [Theory]
    [InlineData("Draft", "NotRequired", false)]
    [InlineData("Upcoming", "NotRequired", true)]
    [InlineData("Upcoming", "Pending", false)]
    [InlineData("Active", "NotRequired", false)]
    [InlineData("Completed", "NotRequired", false)]
    public async Task SuggestOptions_CanCreateBookingFlag_MatchesRealBusinessState(
        string businessState, string approvalState, bool expected)
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 25, businessState: businessState, approvalState: approvalState);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(ValidOptionsJson);

        var result = await Service(db, claude, prompts).SuggestOptionsAsync(25);

        Assert.Equal(expected, result.CanCreateBooking);
    }

    [Theory]
    [InlineData("Draft", "NotRequired", false)]
    [InlineData("Upcoming", "NotRequired", true)]
    [InlineData("Active", "NotRequired", false)]
    public async Task CompareOptions_CanCreateBookingFlag_MatchesRealBusinessState(
        string businessState, string approvalState, bool expected)
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 26, businessState: businessState, approvalState: approvalState);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"comparedOptions":[{"aiComment":"A","recommended":true},{"aiComment":"B","recommended":false}]}""");
        var request = new TravelAiCompareOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight" }, new CreateTravelBookingDto { BookingType = "Hotel" } },
        };

        var result = await Service(db, claude, prompts).CompareOptionsAsync(26, request);

        Assert.Equal(expected, result.CanCreateBooking);
    }

    // Confirm with zero options on an unknown travel request must still 404 (found via a
    // live run: CreateAsync's own existence check never runs when the loop is empty).
    [Fact]
    public async Task ConfirmOptions_ZeroOptions_OnUnknownTravelRequest_ThrowsNotFound()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();

        await Assert.ThrowsAsync<NotFoundException>(
            () => Service(db, claude, prompts).ConfirmOptionsAsync(999999, new ConfirmTravelAiOptionsRequestDto()));
    }

    // Confirm with zero options on a REAL travel request still succeeds (zero is valid).
    [Fact]
    public async Task ConfirmOptions_ZeroOptions_OnRealTravelRequest_SucceedsWithEmptyResult()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 10);
        var (claude, prompts) = Mocks();

        var result = await Service(db, claude, prompts).ConfirmOptionsAsync(10, new ConfirmTravelAiOptionsRequestDto());

        Assert.Empty(result.CreatedBookings);
    }

    // 9. Multiple options -> multiple bookings
    [Fact]
    public async Task ConfirmOptions_MultipleOptions_CreatesOneBookingEach()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 7);
        var (claude, prompts) = Mocks();
        var request = new ConfirmTravelAiOptionsRequestDto
        {
            Options =
            {
                new CreateTravelBookingDto { BookingType = "Flight" },
                new CreateTravelBookingDto { BookingType = "Hotel" },
            },
        };

        var result = await Service(db, claude, prompts).ConfirmOptionsAsync(7, request);

        Assert.Equal(2, result.CreatedBookings.Count);
        Assert.Equal(2, await db.TravelBookings.CountAsync(b => b.TravelRequestId == 7));
    }

    // Confirming multiple options is now all-or-nothing at the database level: ConfirmOptionsAsync
    // opens one ambient transaction around the whole loop, and TravelBookingService.CreateAsync
    // joins it (its ownsTransaction guard) instead of opening its own per option, so a later
    // option failing rolls back every booking from that same call — verified against the real
    // PostgreSQL database, since EF Core's InMemory provider used by this test class does not
    // implement transaction rollback at all (confirmed empirically: SaveChanges applies
    // immediately regardless of an uncommitted ambient transaction, which is exactly what
    // ConfigureWarnings(... TransactionIgnoredWarning) above already acknowledges). This test
    // only proves the exception still propagates correctly; it cannot exercise the rollback
    // itself on this provider.
    [Fact]
    public async Task ConfirmOptions_SecondOptionInvalid_ThrowsAndCreatesNoFurtherBookings()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 27);
        var (claude, prompts) = Mocks();
        var request = new ConfirmTravelAiOptionsRequestDto
        {
            Options =
            {
                new CreateTravelBookingDto { BookingType = "Flight" }, // valid
                new CreateTravelBookingDto { BookingType = "NotARealType" }, // rejected by TravelBookingService itself
            },
        };

        await Assert.ThrowsAsync<BadRequestException>(() => Service(db, claude, prompts).ConfirmOptionsAsync(27, request));

        // The invalid second option must never itself produce a booking, regardless of provider.
        Assert.DoesNotContain(await db.TravelBookings.Where(b => b.TravelRequestId == 27).ToListAsync(),
            b => b.BookingType == "NotARealType");
    }

    // 10-11. Itinerary draft: uses existing bookings as context, persists nothing
    [Fact]
    public async Task DraftItinerary_UsesBookingsContext_AndPersistsNothing()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 8);
        var (claude, prompts) = Mocks();
        await Service(db, claude, prompts).ConfirmOptionsAsync(8, new ConfirmTravelAiOptionsRequestDto
        {
            Options = { new CreateTravelBookingDto { BookingType = "Flight", Provider = "Air India" } },
        });
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"itinerary":"Day 1: Depart Mumbai via Air India."}""");
        var before = await db.TravelRequests.AsNoTracking().SingleAsync(t => t.Id == 8);

        var result = await Service(db, claude, prompts).DraftItineraryAsync(8);

        Assert.Equal("Day 1: Depart Mumbai via Air India.", result.Itinerary);
        prompts.Verify(p => p.BuildItineraryUserPrompt(
            It.IsAny<TravelRequest>(),
            It.Is<List<TravelBookingResponseDto>>(l => l.Count == 1 && l[0].Provider == "Air India")), Times.Once);
        var after = await db.TravelRequests.AsNoTracking().SingleAsync(t => t.Id == 8);
        Assert.Equal(before.ItineraryNotes, after.ItineraryNotes);
        Assert.Null(after.ItineraryNotes); // AI never wrote to it
    }

    // 12. Checklist draft: returns items, filters blanks
    [Fact]
    public async Task DraftChecklist_ReturnsItems_FiltersBlankEntries()
    {
        await using var db = NewDb();
        SeedTravelRequest(db, 9);
        var (claude, prompts) = Mocks();
        claude.Setup(c => c.GenerateJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"items":["Passport","","Travel insurance"]}""");

        var result = await Service(db, claude, prompts).DraftChecklistAsync(9);

        Assert.Equal(new[] { "Passport", "Travel insurance" }, result.ChecklistItems);
    }

    // 13. Unknown travel request for itinerary/checklist
    [Fact]
    public async Task DraftItineraryAndChecklist_UnknownTravelRequest_ThrowNotFound()
    {
        await using var db = NewDb();
        var (claude, prompts) = Mocks();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db, claude, prompts).DraftItineraryAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db, claude, prompts).DraftChecklistAsync(999));
    }

    // 14. Confirm request/response reuse the existing Travel booking DTOs verbatim
    [Fact]
    public void ConfirmRequestAndResponse_ReuseExistingTravelBookingDtos()
    {
        Assert.Equal(typeof(List<CreateTravelBookingDto>), typeof(ConfirmTravelAiOptionsRequestDto).GetProperty(nameof(ConfirmTravelAiOptionsRequestDto.Options))!.PropertyType);
        Assert.Equal(typeof(List<TravelBookingResponseDto>), typeof(TravelAiOptionsConfirmResponseDto).GetProperty(nameof(TravelAiOptionsConfirmResponseDto.CreatedBookings))!.PropertyType);
    }

    // Compare's request also reuses CreateTravelBookingDto — no parallel input shape.
    [Fact]
    public void CompareRequest_ReusesExistingCreateTravelBookingDto()
    {
        Assert.Equal(typeof(List<CreateTravelBookingDto>), typeof(TravelAiCompareOptionsRequestDto).GetProperty(nameof(TravelAiCompareOptionsRequestDto.Options))!.PropertyType);
    }
}
