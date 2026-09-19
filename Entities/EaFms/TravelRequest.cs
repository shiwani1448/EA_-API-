namespace Jarvis5.Entities.EaFms;

/// <summary>
/// Travel and hospitality request. Booking, expense, attachment and workflow execution
/// data remain in their existing shared infrastructure or later modules.
/// </summary>
public class TravelRequest
{
    public long Id { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public long EaTaskId { get; set; }
    public EaTask EaTask { get; set; } = null!;
    public int CurrentCycleNo { get; set; }

    // Traveller rows (name, employee/person id, department, contact information per
    // traveller) live in ea_travel_travellers. The legacy scalar columns
    // (TravellerName, TravellerNames, EmployeePersonId, Department, ContactInformation)
    // remain physically in ea_travel_requests for safety but are no longer mapped or written.
    public ICollection<TravelTraveller> Travellers { get; set; } = new List<TravelTraveller>();

    public string? Purpose { get; set; }
    public string? TravelType { get; set; }
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public DateTime? DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int? NumberOfTravellers { get; set; }
    public string? Priority { get; set; }
    public string? SpecialRequirements { get; set; }
    public DateTime? RequiredDate { get; set; }

    public string? TransportType { get; set; }
    public DateTime? PreferredDeparture { get; set; }
    public DateTime? PreferredArrival { get; set; }
    public string? ClassPreference { get; set; }
    public string? BookingRequirements { get; set; }

    public string? Hotel { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public int? NumberOfRooms { get; set; }
    public string? RoomPreference { get; set; }
    public string? LocationPreference { get; set; }

    public bool? PickupRequired { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropLocation { get; set; }
    public string? VehiclePreference { get; set; }

    public string? ClientGuestDetails { get; set; }
    public string? HospitalityRequirement { get; set; }
    public string? MeetingEventPurpose { get; set; }
    public int? NumberOfGuests { get; set; }
    public string? SpecialArrangements { get; set; }
    public string? ItineraryNotes { get; set; }
    public string? AdditionalInstructions { get; set; }

    public decimal? EstimatedTravelCost { get; set; }
    public decimal? EstimatedHotelCost { get; set; }
    public decimal? EstimatedLocalTransportCost { get; set; }
    public decimal? EstimatedHospitalityCost { get; set; }
    public string? Currency { get; set; }

    public bool ApprovalRequired { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverNameSnapshot { get; set; }
    public string BusinessState { get; set; } = "Draft";
    public string ApprovalState { get; set; } = "NotRequired";

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    // Actual operational trip-commencement time (Upcoming -> Active), distinct from
    // SubmittedAt (approval-cycle submission) and RequiredDate (business target date).
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<TravelRequestCycle> Cycles { get; set; } = new List<TravelRequestCycle>();
}
