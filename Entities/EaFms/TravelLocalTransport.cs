namespace Jarvis5.Entities.EaFms;

// Operational arrangement; request-time requirements remain on TravelRequest.
public class TravelLocalTransport
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }
    public TravelRequest TravelRequest { get; set; } = null!;
    public string TransportStatus { get; set; } = "Requested";
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
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
