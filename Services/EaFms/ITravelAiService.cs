using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface ITravelAiService
{
    /// <summary>Preview-only: suggests candidate travel/hotel options from the Travel
    /// Request's own stated route/dates/preferences. Estimated costs are illustrative only
    /// (Claude has no live pricing). Never writes to the database.</summary>
    Task<TravelAiOptionsSuggestionResponseDto> SuggestOptionsAsync(long travelRequestId, CancellationToken ct = default);

    /// <summary>EA confirmation of (possibly edited) suggested options: creates one real
    /// TravelBooking per submitted entry by calling the EXISTING
    /// ITravelBookingService.CreateAsync directly — no parallel creation path, no new
    /// validation, no new transaction scope. Never calls Claude.</summary>
    Task<TravelAiOptionsConfirmResponseDto> ConfirmOptionsAsync(long travelRequestId, ConfirmTravelAiOptionsRequestDto dto, CancellationToken ct = default);

    /// <summary>Preview-only: compares EA-supplied, already-real-priced options (she found
    /// these herself) against the trip's stated preferences and recommends one. Claude
    /// never alters a submitted price/provider/detail. Never writes to the database — the
    /// EA still uses ConfirmOptionsAsync afterward to create a real TravelBooking.</summary>
    Task<TravelAiCompareOptionsResponseDto> CompareOptionsAsync(long travelRequestId, TravelAiCompareOptionsRequestDto dto, CancellationToken ct = default);

    /// <summary>Preview-only: drafts a narrative itinerary from the request's details and
    /// any already-created bookings. Never writes to the database — if the EA wants to
    /// keep it, they save it themselves via the existing Travel Request update endpoint
    /// (ItineraryNotes), exactly like a manually typed itinerary.</summary>
    Task<TravelAiItineraryResponseDto> DraftItineraryAsync(long travelRequestId, CancellationToken ct = default);

    /// <summary>Preview-only: drafts a packing/document checklist. Purely advisory —
    /// there is nothing to persist and no confirm step.</summary>
    Task<TravelAiChecklistResponseDto> DraftChecklistAsync(long travelRequestId, CancellationToken ct = default);
}
