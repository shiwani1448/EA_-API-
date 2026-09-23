using System.Text;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;

namespace Jarvis5.Services.EaFms;

public class TravelAiPromptBuilder : ITravelAiPromptBuilder
{
    // ============================================================
    // Options suggestion
    // ============================================================

    public string BuildOptionsSystemPrompt() => """
        You help an Executive Assistant plan business travel by suggesting candidate
        transport and hotel options for a travel request.

        Strict rules:
        - Only suggest option TYPES from this exact list: Flight, Train, RoadCar, Hotel, LocalTransport.
        - Never invent a real booking reference, confirmation number, or exact live price —
          you have no access to real-time fares or availability. Estimated costs must be
          described as rough, illustrative ranges based on general knowledge only.
        - Never claim a provider guarantees availability.
        - Base suggestions only on the route, dates and preferences given below — do not
          invent travellers, purposes, or requirements not stated.
        - Use null for any field you cannot reasonably estimate.
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildOptionsUserPrompt(TravelRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TRAVEL REQUEST DETAILS");
        sb.AppendLine($"From: {request.FromLocation ?? "(not specified)"}");
        sb.AppendLine($"To: {request.ToLocation ?? "(not specified)"}");
        sb.AppendLine($"Departure: {request.DepartureDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}");
        sb.AppendLine($"Return: {request.ReturnDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}");
        sb.AppendLine($"Transport type preference: {request.TransportType ?? "(none stated)"}");
        sb.AppendLine($"Class preference: {request.ClassPreference ?? "(none stated)"}");
        sb.AppendLine($"Number of travellers: {request.NumberOfTravellers?.ToString() ?? "(not specified)"}");
        sb.AppendLine($"Hotel: {request.Hotel ?? "(none stated)"}");
        sb.AppendLine($"Check-in: {request.CheckInDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}, Check-out: {request.CheckOutDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}");
        sb.AppendLine($"Room preference: {request.RoomPreference ?? "(none stated)"}");
        sb.AppendLine($"Location preference: {request.LocationPreference ?? "(none stated)"}");
        sb.AppendLine($"Special requirements: {request.SpecialRequirements ?? "(none)"}");
        sb.AppendLine($"Estimated travel budget: {request.EstimatedTravelCost?.ToString() ?? "(not specified)"} {request.Currency}");
        sb.AppendLine($"Estimated hotel budget: {request.EstimatedHotelCost?.ToString() ?? "(not specified)"} {request.Currency}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Suggest 2 to 4 candidate options covering what's relevant (transport and/or hotel). " +
            "Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""
            {
              "proposedOptions": [
                {
                  "bookingType": "Flight|Train|RoadCar|Hotel|LocalTransport",
                  "provider": string|null,
                  "estimatedCost": number|null,
                  "currency": string|null,
                  "details": string|null,
                  "notes": string|null
                }
              ]
            }
            """);
        return sb.ToString();
    }

    // ============================================================
    // Compare EA-supplied options
    // ============================================================

    public string BuildCompareSystemPrompt() => """
        You compare REAL travel/hotel options a human has already found and priced for a
        business trip.

        Strict rules:
        - Do NOT change, correct, or second-guess any price, provider, or detail given to
          you — treat every value as already verified and real.
        - Do NOT invent a new option, and do NOT drop any of the given options.
        - Evaluate the given options only against the trip's stated dates/purpose/
          preferences below (timing convenience, fit with the trip, and price).
        - Return exactly one comment per option, in the SAME ORDER they were given.
        - Mark exactly one option as recommended (the single best overall choice).
        - Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildCompareUserPrompt(TravelRequest request, List<CreateTravelBookingDto> options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TRIP CONTEXT");
        sb.AppendLine($"From: {request.FromLocation ?? "(not specified)"} To: {request.ToLocation ?? "(not specified)"}");
        sb.AppendLine($"Departure: {request.DepartureDate?.ToString("yyyy-MM-dd") ?? "(not specified)"} Return: {request.ReturnDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}");
        sb.AppendLine($"Purpose: {request.Purpose ?? "(not specified)"}");
        sb.AppendLine($"Class preference: {request.ClassPreference ?? "(none stated)"}");
        sb.AppendLine($"Room preference: {request.RoomPreference ?? "(none stated)"}");
        sb.AppendLine($"Location preference: {request.LocationPreference ?? "(none stated)"}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("OPTIONS TO COMPARE (real, already priced by the EA — do not alter any value)");
        for (var i = 0; i < options.Count; i++)
        {
            var o = options[i];
            sb.AppendLine($"Option {i + 1}: bookingType={o.BookingType}, provider={o.Provider ?? "(not specified)"}, " +
                $"cost={o.Cost?.ToString() ?? "(not specified)"} {o.Currency}, " +
                $"departure={o.DepartureDetails ?? "(n/a)"}, arrival={o.ArrivalDetails ?? "(n/a)"}, " +
                $"hotel={o.HotelDetails ?? "(n/a)"}, vehicle={o.VehicleDetails ?? "(n/a)"}, notes={o.Notes ?? "(none)"}");
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine($"Return ONLY a single JSON object with exactly {options.Count} entries, " +
            "in the same order as the options above:");
        sb.AppendLine("""
            {
              "comparedOptions": [
                { "aiComment": string, "recommended": boolean }
              ]
            }
            """);
        return sb.ToString();
    }

    // ============================================================
    // Itinerary draft
    // ============================================================

    public string BuildItinerarySystemPrompt() => """
        You write a concise day-by-day travel itinerary for an Executive Assistant to review.
        Use only the travel request details and confirmed bookings given below — never invent
        flight numbers, hotel names, or times that were not provided. Where a detail is
        missing, write "TBD" rather than guessing. Return ONLY a single valid JSON object.
        Never return markdown or prose outside JSON.
        """;

    public string BuildItineraryUserPrompt(TravelRequest request, List<TravelBookingResponseDto> bookings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TRAVEL REQUEST");
        sb.AppendLine($"From: {request.FromLocation ?? "(not specified)"} To: {request.ToLocation ?? "(not specified)"}");
        sb.AppendLine($"Departure: {request.DepartureDate?.ToString("yyyy-MM-dd") ?? "TBD"} Return: {request.ReturnDate?.ToString("yyyy-MM-dd") ?? "TBD"}");
        sb.AppendLine($"Purpose: {request.Purpose ?? "(not specified)"}");
        sb.AppendLine($"Meeting/event purpose: {request.MeetingEventPurpose ?? "(none)"}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("CONFIRMED BOOKINGS");
        if (bookings.Count == 0)
        {
            sb.AppendLine("No bookings confirmed yet.");
        }
        else
        {
            foreach (var b in bookings)
                sb.AppendLine($"- {b.BookingType}: {b.Provider ?? "(provider TBD)"} | {b.DepartureDetails ?? b.HotelDetails ?? b.VehicleDetails ?? "(details TBD)"}");
        }

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "itinerary": string }""");
        return sb.ToString();
    }

    // ============================================================
    // Checklist draft
    // ============================================================

    public string BuildChecklistSystemPrompt() => """
        You produce a short packing/documents checklist for a business trip, based only on
        the destination, dates and trip type given below. Never invent visa/document
        requirements you are not reasonably confident about — phrase anything uncertain as
        "verify visa/document requirements for <destination>" rather than stating it as fact.
        Return ONLY a single valid JSON object. Never return markdown or prose outside JSON.
        """;

    public string BuildChecklistUserPrompt(TravelRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"From: {request.FromLocation ?? "(not specified)"} To: {request.ToLocation ?? "(not specified)"}");
        sb.AppendLine($"Departure: {request.DepartureDate?.ToString("yyyy-MM-dd") ?? "(not specified)"} Return: {request.ReturnDate?.ToString("yyyy-MM-dd") ?? "(not specified)"}");
        sb.AppendLine($"Travel type: {request.TravelType ?? "(not specified)"}");
        sb.AppendLine($"Purpose: {request.Purpose ?? "(not specified)"}");
        sb.AppendLine($"Special requirements: {request.SpecialRequirements ?? "(none)"}");

        sb.AppendLine("------------------------------------------------");
        sb.AppendLine("Return ONLY a single JSON object with exactly this shape:");
        sb.AppendLine("""{ "items": [string] }""");
        return sb.ToString();
    }
}
