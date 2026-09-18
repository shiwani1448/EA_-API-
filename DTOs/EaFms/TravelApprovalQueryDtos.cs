namespace Jarvis5.Dtos.EaFms;

public class TravelPendingApprovalQueryDto
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class TravelPendingApprovalDto
{
    public long TravelRequestId { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string? TravellerName { get; set; }
    public string? Department { get; set; }
    public string? Purpose { get; set; }
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public DateTime? DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Priority { get; set; }
    public string BusinessState { get; set; } = string.Empty;
    public string ApprovalState { get; set; } = string.Empty;
    public int CurrentCycleNo { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public string? Currency { get; set; }
    public int DocumentCount { get; set; }
}

// Reuses Travel detail groups without exposing task or internal cycle identifiers.
public class TravelApprovalDetailDto
{
    public long TravelRequestId { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string BusinessState { get; set; } = string.Empty;
    public string ApprovalState { get; set; } = string.Empty;
    public int CurrentCycleNo { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public TravelTravellerDto Traveller { get; set; } = new();
    public TravelTripDto Trip { get; set; } = new();
    public TravelTransportationDto Transportation { get; set; } = new();
    public TravelHotelDto Hotel { get; set; } = new();
    public TravelLocalTransportDto LocalTransport { get; set; } = new();
    public TravelHospitalityDto Hospitality { get; set; } = new();
    public TravelItineraryDto Itinerary { get; set; } = new();
    public TravelBudgetDto Budget { get; set; } = new();
    public TravelApprovalDto Approval { get; set; } = new();
    public TravelCurrentCycleDto? CurrentCycle { get; set; }
    public List<TravelCurrentCycleDto> ApprovalHistory { get; set; } = new();
    public List<TravelDocumentResponseDto> Documents { get; set; } = new();
}
