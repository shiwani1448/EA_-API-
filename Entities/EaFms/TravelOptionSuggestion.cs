namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI options-suggestion result for a Travel Request, shaped exactly like
/// TravelAiOptionsSuggestionResponseDto. See MeetingActionExtraction's doc comment for the
/// full rationale (real columns per real field, jsonb only for the genuinely repeating
/// options list). Applied via /ai/options/confirm, which creates real ea_travel_bookings
/// rows — AppliedBookingIdsJson records exactly which ones.
/// </summary>
public class TravelOptionSuggestion
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }

    /// <summary>List of { bookingType, provider, estimatedCost, currency, details, notes } —
    /// see TravelAiProposedOptionDto.</summary>
    public string ProposedOptionsJson { get; set; } = string.Empty;
    public string? WarningMessage { get; set; }
    public bool CanCreateBooking { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    /// <summary>ea_travel_bookings.Id values actually created via /ai/options/confirm.</summary>
    public string? AppliedBookingIdsJson { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
