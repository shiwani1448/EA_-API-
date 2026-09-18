namespace Jarvis5.Dtos.EaFms;

// PUT replaces optional execution fields; omitted status preserves the current status.
public class UpdateTravelBookingDto
{
    public string? BookingStatus { get; set; }
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
}

public class CreateTravelBookingDto : UpdateTravelBookingDto
{
    public string BookingType { get; set; } = string.Empty;
}

public class TravelBookingResponseDto
{
    public long BookingId { get; set; }
    public long TravelRequestId { get; set; }
    public string TravelReferenceNo { get; set; } = string.Empty;
    public string BookingType { get; set; } = string.Empty;
    public string BookingStatus { get; set; } = string.Empty;
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
