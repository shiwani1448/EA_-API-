namespace Jarvis5.Dtos.EaFms;

// POST/PUT accept only arrangement fields. PUT replaces optional data;
// omitted status preserves its current value. POST defaults status to Requested.
public class SaveTravelHospitalityDto
{
    public string? Status { get; set; }
    public string? ClientGuestDetails { get; set; }
    public string? HospitalityRequirement { get; set; }
    public string? MeetingEventPurpose { get; set; }
    public int? NumberOfGuests { get; set; }
    public string? SpecialArrangements { get; set; }
    public string? Location { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? Provider { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
}

public class TravelHospitalityResponseDto
{
    public long HospitalityId { get; set; }
    public long TravelRequestId { get; set; }
    public string TravelReferenceNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ClientGuestDetails { get; set; }
    public string? HospitalityRequirement { get; set; }
    public string? MeetingEventPurpose { get; set; }
    public int? NumberOfGuests { get; set; }
    public string? SpecialArrangements { get; set; }
    public string? Location { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? Provider { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
