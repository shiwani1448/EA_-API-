namespace Jarvis5.Dtos.EaFms;

// ============================================================
// Options suggestion + confirm (preview -> real TravelBooking)
// ============================================================

/// <summary>Response for POST /api/ea/travel/requests/{travelRequestId}/ai/options/suggest.
/// Preview only — nothing behind this response is persisted.</summary>
public class TravelAiOptionsSuggestionResponseDto
{
    public long TravelRequestId { get; set; }
    public List<TravelAiProposedOptionDto> ProposedOptions { get; set; } = new();

    /// <summary>Always set: estimated costs are illustrative only (Claude has no live
    /// pricing feed), never a real quote.</summary>
    public string? WarningMessage { get; set; }

    /// <summary>Whether /ai/options/confirm would actually succeed right now for this
    /// travel request — the same rule TravelBookingService.CreateAsync itself enforces
    /// (BusinessState "Upcoming" + approval satisfied). Suggest Options is most useful
    /// while still a Draft (to help fill in EstimatedTravelCost/EstimatedHotelCost before
    /// submitting for approval) — this flag tells the frontend whether that's also true
    /// right now, or whether it's too early (still Draft/pending approval) or too late
    /// (already Active/Completed) to actually confirm a booking, without the frontend
    /// re-deriving the business-state rule itself.</summary>
    public bool CanCreateBooking { get; set; }
}

/// <summary>One AI-proposed travel/hotel option, for EA review only. Never a real booking
/// reference or a guaranteed price — the EA picks/edits before it becomes a real
/// TravelBooking via /ai/options/confirm.</summary>
public class TravelAiProposedOptionDto
{
    /// <summary>One of TravelBookingRules' types: Flight | Train | RoadCar | Hotel | LocalTransport.</summary>
    public string? BookingType { get; set; }
    public string? Provider { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string? Currency { get; set; }
    public string? Details { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Request for POST /api/ea/travel/requests/{travelRequestId}/ai/options/confirm.
/// Each entry is the EA-confirmed (possibly edited) final value for one option — reuses
/// CreateTravelBookingDto verbatim, the same shape the manual
/// POST /api/ea/travel/requests/{travelRequestId}/bookings endpoint already accepts.</summary>
public class ConfirmTravelAiOptionsRequestDto
{
    public List<CreateTravelBookingDto> Options { get; set; } = new();
}

/// <summary>Response for .../ai/options/confirm. Reuses the existing
/// TravelBookingResponseDto (the same shape the manual bookings endpoint returns).</summary>
public class TravelAiOptionsConfirmResponseDto
{
    public long TravelRequestId { get; set; }
    public List<TravelBookingResponseDto> CreatedBookings { get; set; } = new();
}

// ============================================================
// Compare EA-supplied options (preview only — no persistence; EA still
// uses the existing /ai/options/confirm to create a real TravelBooking)
// ============================================================

/// <summary>Request for POST /api/ea/travel/requests/{travelRequestId}/ai/options/compare.
/// Every price/detail here is EA-supplied and real (she already searched them herself) —
/// reuses CreateTravelBookingDto verbatim, the same shape /ai/options/confirm and the
/// manual bookings endpoint already accept, so a compared option can be sent straight to
/// confirm afterward with no reshaping.</summary>
public class TravelAiCompareOptionsRequestDto
{
    public List<CreateTravelBookingDto> Options { get; set; } = new();
}

/// <summary>Response for .../ai/options/compare. Echoes each submitted option back
/// unchanged (Claude is never allowed to alter a price/provider/detail) plus its own
/// commentary and whether it's the recommended one.</summary>
public class TravelAiComparedOptionDto
{
    public string? BookingType { get; set; }
    public string? Provider { get; set; }
    public string? DepartureDetails { get; set; }
    public string? ArrivalDetails { get; set; }
    public string? HotelDetails { get; set; }
    public string? VehicleDetails { get; set; }
    public decimal? Cost { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }

    /// <summary>Claude's one-sentence evaluation of this specific option against the
    /// trip's stated dates/purpose/preferences.</summary>
    public string? AiComment { get; set; }
    public bool IsRecommended { get; set; }
}

public class TravelAiCompareOptionsResponseDto
{
    public long TravelRequestId { get; set; }
    public List<TravelAiComparedOptionDto> ComparedOptions { get; set; } = new();

    /// <summary>Same meaning and same rule as TravelAiOptionsSuggestionResponseDto's
    /// CanCreateBooking — whether /ai/options/confirm would succeed right now. Compare is
    /// meant to be used once the request is Upcoming (right before confirming a real
    /// booking); this tells the frontend whether it actually is.</summary>
    public bool CanCreateBooking { get; set; }
}

// ============================================================
// Itinerary draft (preview only — no confirm step; EA reuses the
// existing Travel Request update endpoint to save ItineraryNotes)
// ============================================================

public class TravelAiItineraryResponseDto
{
    public long TravelRequestId { get; set; }
    public string? Itinerary { get; set; }
}

// ============================================================
// Checklist draft (preview only — advisory, nothing to persist)
// ============================================================

public class TravelAiChecklistResponseDto
{
    public long TravelRequestId { get; set; }
    public List<string> ChecklistItems { get; set; } = new();
}
