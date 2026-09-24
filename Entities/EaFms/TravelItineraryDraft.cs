namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI itinerary draft for a Travel Request, shaped exactly like
/// TravelAiItineraryResponseDto — a single narrative text field, so a real scalar column
/// (no jsonb needed at all). Purely advisory: there is no confirm/apply step for this in the
/// real flow — if the EA wants to keep it, they copy it themselves into the Travel
/// Request's own ItineraryNotes field via the existing (non-AI) update endpoint, so there is
/// nothing here to mark as "applied".
/// </summary>
public class TravelItineraryDraft
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }

    public string? Itinerary { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
