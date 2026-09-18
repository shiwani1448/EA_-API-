namespace Jarvis5.Entities.EaFms;

public class TravelBooking
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }
    public TravelRequest TravelRequest { get; set; } = null!;
    public string BookingType { get; set; } = string.Empty;
    public string BookingStatus { get; set; } = "Requested";
    public string? Provider { get; set; }
    public string? BookingReference { get; set; }
    public DateTime? BookingDate { get; set; }
    public string? DepartureDetails { get; set; }
    public string? ArrivalDetails { get; set; }
    public string? HotelDetails { get; set; }
    public string? VehicleDetails { get; set; }
    public decimal? Cost { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
}
