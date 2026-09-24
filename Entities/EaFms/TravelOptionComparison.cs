namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI options-comparison result for a Travel Request, shaped exactly like
/// TravelAiCompareOptionsResponseDto. See MeetingActionExtraction's doc comment for the
/// full rationale. Applied via /ai/options/confirm, exactly like TravelOptionSuggestion.
/// </summary>
public class TravelOptionComparison
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }

    /// <summary>List of the EA-supplied options echoed back plus aiComment/isRecommended —
    /// see TravelAiComparedOptionDto. Prices/providers are the EA's own real values, never
    /// altered by Claude — only aiComment/isRecommended are AI-added.</summary>
    public string ComparedOptionsJson { get; set; } = string.Empty;
    public bool CanCreateBooking { get; set; }

    public bool IsApplied { get; set; }
    public DateTime? AppliedAt { get; set; }
    /// <summary>ea_travel_bookings.Id values actually created via /ai/options/confirm.</summary>
    public string? AppliedBookingIdsJson { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
