namespace Jarvis5.Dtos.EaFms;

// POST/PUT accept only arrangement fields. PUT replaces optional data;
// omitted status preserves its current value. POST defaults status to Requested.
public class SaveTravelLocalTransportDto
{
    public string? TransportStatus { get; set; }
    public string? TransportType { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropLocation { get; set; }
    public string? VehiclePreference { get; set; }
    public string? BookingReference { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? Provider { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
}

public class TravelLocalTransportResponseDto
{
    public long LocalTransportId { get; set; }
    public long TravelRequestId { get; set; }
    public string TravelReferenceNo { get; set; } = string.Empty;
    public string TransportStatus { get; set; } = string.Empty;
    public string? TransportType { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropLocation { get; set; }
    public string? VehiclePreference { get; set; }
    public string? BookingReference { get; set; }
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
