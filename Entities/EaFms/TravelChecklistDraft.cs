namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One AI checklist draft for a Travel Request, shaped exactly like
/// TravelAiChecklistResponseDto. ChecklistItemsJson is jsonb because it is genuinely a list
/// of strings — see MeetingActionExtraction's doc comment. Purely advisory, same as
/// TravelItineraryDraft: no confirm/apply step exists for this in the real flow.
/// </summary>
public class TravelChecklistDraft
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }

    /// <summary>List of checklist item strings.</summary>
    public string ChecklistItemsJson { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
