using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;

namespace Jarvis5.Services.EaFms;

/// <summary>Builds the system/user prompts for the three Travel AI capabilities (options
/// suggestion, itinerary draft, checklist draft) — the same hardcoded-prompt-builder
/// pattern already used by Meeting/SCIH. Contains no Claude/HTTP logic.</summary>
public interface ITravelAiPromptBuilder
{
    string BuildOptionsSystemPrompt();
    string BuildOptionsUserPrompt(TravelRequest request);

    string BuildCompareSystemPrompt();
    string BuildCompareUserPrompt(TravelRequest request, List<CreateTravelBookingDto> options);

    string BuildItinerarySystemPrompt();
    string BuildItineraryUserPrompt(TravelRequest request, List<TravelBookingResponseDto> bookings);

    string BuildChecklistSystemPrompt();
    string BuildChecklistUserPrompt(TravelRequest request);
}
