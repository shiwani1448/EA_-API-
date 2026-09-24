using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Preview-only AI assistance for Travel & Hospitality (options suggestion, itinerary
/// draft, checklist draft) plus EA confirmation of suggested options into real bookings.
/// Orchestration only — provider-specific logic (Claude, JSON parsing) lives in the
/// already-registered shared services; this class never talks to Claude directly.
///
/// ConfirmOptionsAsync creates real TravelBooking rows by calling the EXISTING
/// ITravelBookingService.CreateAsync — the exact same creation path, validation
/// (BookingType/BookingStatus rules) and business-state precondition (EnsureReady: the
/// Travel Request must already be "Upcoming" and approved/NotRequired) as the manual
/// POST /api/ea/travel/requests/{id}/bookings endpoint. No parallel booking-creation path,
/// no schema change, no change to Travel lifecycle/approval/TAT.
/// </summary>
public class TravelAiService : ITravelAiService
{
    private readonly EaFmsDbContext _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITravelAiPromptBuilder _promptBuilder;
    private readonly ITravelBookingService _bookings;
    private readonly ILogger<TravelAiService> _logger;
    private readonly int _maxAiAttempts;

    // IClaudeClient is resolved lazily via IServiceProvider (not a direct constructor
    // dependency) — same reason as MeetingAiService: IClaudeClient is a singleton whose
    // constructor throws if AnthropicSettings:ApiKey is missing, and ConfirmOptionsAsync
    // never calls Claude at all, so it must not be forced to fail in an environment
    // without a configured key.
    public TravelAiService(
        EaFmsDbContext db,
        IServiceProvider serviceProvider,
        ITravelAiPromptBuilder promptBuilder,
        ITravelBookingService bookings,
        ILogger<TravelAiService> logger,
        IOptions<ClaudeOptions> claudeOptions)
    {
        _db = db;
        _serviceProvider = serviceProvider;
        _promptBuilder = promptBuilder;
        _bookings = bookings;
        _logger = logger;
        _maxAiAttempts = Math.Clamp(claudeOptions.Value.MaxRetries, 1, 3);
    }

    private async Task<TravelRequest> LoadAsync(long travelRequestId, CancellationToken ct) =>
        await _db.TravelRequests.AsNoTracking().SingleOrDefaultAsync(t => t.Id == travelRequestId && !t.IsDeleted, ct)
        ?? throw new NotFoundException($"Travel request {travelRequestId} not found.");

    public async Task<TravelAiOptionsSuggestionResponseDto> SuggestOptionsAsync(long travelRequestId, CancellationToken ct = default)
    {
        var request = await LoadAsync(travelRequestId, ct);

        var hasAnyInput = !string.IsNullOrWhiteSpace(request.FromLocation)
            || !string.IsNullOrWhiteSpace(request.ToLocation)
            || !string.IsNullOrWhiteSpace(request.Hotel)
            || request.DepartureDate.HasValue
            || request.CheckInDate.HasValue;
        if (!hasAnyInput)
            throw new BusinessRuleException("Travel request has no route, dates or hotel preference to suggest options from.");

        var systemPrompt = _promptBuilder.BuildOptionsSystemPrompt();
        var userPrompt = _promptBuilder.BuildOptionsUserPrompt(request);

        var aiResult = await GenerateAndParseAsync<TravelAiOptionsResultDto>(
            systemPrompt, userPrompt, "Travel option suggestion", ct);

        var response = new TravelAiOptionsSuggestionResponseDto
        {
            TravelRequestId = travelRequestId,
            ProposedOptions = aiResult.ProposedOptions
                .Where(o => !string.IsNullOrWhiteSpace(o.BookingType))
                .Select(o => new TravelAiProposedOptionDto
                {
                    BookingType = o.BookingType,
                    Provider = o.Provider,
                    EstimatedCost = o.EstimatedCost,
                    Currency = o.Currency,
                    Details = o.Details,
                    Notes = o.Notes,
                })
                .ToList(),
            WarningMessage = "Estimated costs are illustrative only, not live pricing — verify with the provider before booking.",
            CanCreateBooking = TravelBookingService.IsReadyForBooking(request),
        };
        await AiSuggestionWriters.LogTravelOptionSuggestionAsync(_db, travelRequestId, response, ct);
        return response;
    }

    public async Task<TravelAiOptionsConfirmResponseDto> ConfirmOptionsAsync(
        long travelRequestId, ConfirmTravelAiOptionsRequestDto dto, CancellationToken ct = default)
    {
        // Existence is otherwise enforced by ITravelBookingService.CreateAsync itself for
        // every submitted option (NotFoundException for an unknown request,
        // BusinessRuleException via EnsureReady for a request that isn't Upcoming/approved)
        // — but that check never runs at all when zero options are submitted, which would
        // silently return an empty 200 for a nonexistent travel request (found via a live
        // local run: POST .../999999/ai/options/confirm with an empty body returned 200
        // {"createdBookings":[]} instead of 404). This explicit check only covers the
        // zero-options case; CreateAsync's own check still governs whenever at least one
        // option is submitted, so there is no duplicated/drifting business rule.
        if (dto.Options.Count == 0 && !await _db.TravelRequests.AnyAsync(t => t.Id == travelRequestId && !t.IsDeleted, ct))
            throw new NotFoundException($"Travel request {travelRequestId} not found.");

        var created = new List<TravelBookingResponseDto>();
        if (dto.Options.Count > 0)
        {
            // All-or-nothing across every submitted option: without this, a later option
            // failing (e.g. an unsupported bookingType) would leave the earlier ones already
            // committed as real bookings. TravelBookingService.CreateAsync joins this ambient
            // transaction instead of opening its own, so nothing commits until every option
            // in this call has succeeded.
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            foreach (var option in dto.Options)
            {
                created.Add(await _bookings.CreateAsync(travelRequestId, option, ct));
            }
            await tx.CommitAsync(ct);
        }

        var response = new TravelAiOptionsConfirmResponseDto
        {
            TravelRequestId = travelRequestId,
            CreatedBookings = created,
        };
        if (created.Count > 0)
        {
            // Links this confirmation back to whichever single suggestion led to it — Suggest
            // or Compare, whichever was generated more recently — since Confirm can follow
            // either. A no-op if the EA typed the options manually without calling either first.
            await AiSuggestionWriters.MarkLatestTravelOptionsAppliedAsync(_db, travelRequestId, response, ct);
        }
        return response;
    }

    public async Task<TravelAiCompareOptionsResponseDto> CompareOptionsAsync(
        long travelRequestId, TravelAiCompareOptionsRequestDto dto, CancellationToken ct = default)
    {
        var request = await LoadAsync(travelRequestId, ct);

        if (dto.Options.Count < 2)
            throw new BusinessRuleException("At least two options are required to compare.");

        var systemPrompt = _promptBuilder.BuildCompareSystemPrompt();
        var userPrompt = _promptBuilder.BuildCompareUserPrompt(request, dto.Options);

        var aiResult = await GenerateAndParseAsync<TravelAiCompareResultDto>(
            systemPrompt, userPrompt, "Travel option comparison", ct);

        if (aiResult.ComparedOptions.Count != dto.Options.Count)
            throw new BusinessRuleException(
                "AI comparison did not return exactly one result per submitted option.");

        var compared = new List<TravelAiComparedOptionDto>();
        for (var i = 0; i < dto.Options.Count; i++)
        {
            var o = dto.Options[i];
            var c = aiResult.ComparedOptions[i];
            compared.Add(new TravelAiComparedOptionDto
            {
                BookingType = o.BookingType,
                Provider = o.Provider,
                DepartureDetails = o.DepartureDetails,
                ArrivalDetails = o.ArrivalDetails,
                HotelDetails = o.HotelDetails,
                VehicleDetails = o.VehicleDetails,
                Cost = o.Cost,
                Currency = o.Currency,
                Notes = o.Notes,
                AiComment = c.AiComment,
                IsRecommended = c.Recommended,
            });
        }

        var response = new TravelAiCompareOptionsResponseDto
        {
            TravelRequestId = travelRequestId,
            ComparedOptions = compared,
            CanCreateBooking = TravelBookingService.IsReadyForBooking(request),
        };
        await AiSuggestionWriters.LogTravelOptionComparisonAsync(_db, travelRequestId, response, ct);
        return response;
    }

    public async Task<TravelAiItineraryResponseDto> DraftItineraryAsync(long travelRequestId, CancellationToken ct = default)
    {
        var request = await LoadAsync(travelRequestId, ct);
        var bookings = await _bookings.ListAsync(travelRequestId, ct);

        var systemPrompt = _promptBuilder.BuildItinerarySystemPrompt();
        var userPrompt = _promptBuilder.BuildItineraryUserPrompt(request, bookings);

        var aiResult = await GenerateAndParseAsync<TravelAiItineraryResultDto>(
            systemPrompt, userPrompt, "Travel itinerary draft", ct);

        var response = new TravelAiItineraryResponseDto
        {
            TravelRequestId = travelRequestId,
            Itinerary = aiResult.Itinerary,
        };
        await AiSuggestionWriters.LogTravelItineraryDraftAsync(_db, travelRequestId, response, ct);
        return response;
    }

    public async Task<TravelAiChecklistResponseDto> DraftChecklistAsync(long travelRequestId, CancellationToken ct = default)
    {
        var request = await LoadAsync(travelRequestId, ct);

        var systemPrompt = _promptBuilder.BuildChecklistSystemPrompt();
        var userPrompt = _promptBuilder.BuildChecklistUserPrompt(request);

        var aiResult = await GenerateAndParseAsync<TravelAiChecklistResultDto>(
            systemPrompt, userPrompt, "Travel checklist", ct);

        var response = new TravelAiChecklistResponseDto
        {
            TravelRequestId = travelRequestId,
            ChecklistItems = aiResult.Items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList(),
        };
        await AiSuggestionWriters.LogTravelChecklistDraftAsync(_db, travelRequestId, response, ct);
        return response;
    }

    // Same retry-on-malformed-JSON pattern as MeetingAiService/AnalysisService.
    private async Task<T> GenerateAndParseAsync<T>(
        string systemPrompt,
        string userPrompt,
        string entityName,
        CancellationToken ct) where T : new()
    {
        BusinessRuleException? lastParseError = null;

        for (var attempt = 1; attempt <= _maxAiAttempts; attempt++)
        {
            var attemptPrompt = attempt == 1
                ? userPrompt
                : userPrompt + """

                    IMPORTANT RETRY: The prior response was not valid JSON. Return one complete, compact JSON
                    object only. Do not use markdown fences, comments, smart quotes, trailing commas, or text
                    before/after the object.
                    """;

            var claudeClient = _serviceProvider.GetRequiredService<IClaudeClient>();
            var rawResponse = await claudeClient.GenerateJsonAsync(systemPrompt, attemptPrompt, ct);

            try
            {
                return AiJsonResponseParser.Parse<T>(rawResponse, _logger, entityName);
            }
            catch (BusinessRuleException ex) when (attempt < _maxAiAttempts)
            {
                lastParseError = ex;
                _logger.LogWarning(
                    "AI {Entity} returned invalid JSON on attempt {Attempt}/{MaxAttempts}; retrying.",
                    entityName, attempt, _maxAiAttempts);
            }
        }

        throw lastParseError
            ?? new BusinessRuleException($"AI {entityName} service did not return valid JSON.");
    }
}

// Internal shapes for parsing Claude's raw JSON only — never returned from the public API.
internal class TravelAiOptionsResultDto
{
    public List<TravelAiOptionResultItemDto> ProposedOptions { get; set; } = new();
}

internal class TravelAiOptionResultItemDto
{
    public string? BookingType { get; set; }
    public string? Provider { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string? Currency { get; set; }
    public string? Details { get; set; }
    public string? Notes { get; set; }
}

internal class TravelAiCompareResultDto
{
    public List<TravelAiCompareResultItemDto> ComparedOptions { get; set; } = new();
}

internal class TravelAiCompareResultItemDto
{
    public string? AiComment { get; set; }
    public bool Recommended { get; set; }
}

internal class TravelAiItineraryResultDto
{
    public string? Itinerary { get; set; }
}

internal class TravelAiChecklistResultDto
{
    public List<string> Items { get; set; } = new();
}
