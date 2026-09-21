namespace Jarvis5.Dtos.EaFms;

// ============================================================
// CREATE DTO
// ============================================================

/// <summary>
/// Fields the frontend may supply when creating a new Travel Draft.
/// Backend-owned fields (Id, ReferenceNo, EaTaskId, CurrentCycleNo, BusinessState,
/// ApprovalState, TotalEstimatedCost, ApproverNameSnapshot, audit timestamps,
/// WorkflowInstanceId) are NOT present here.
/// </summary>
public class CreateTravelRequestDto
{
    /// <summary>
    /// Stable identity of the logged-in EA frontend user creating this request — the
    /// same User.Id already returned by GET /api/Users. EA APIs do not use JWT, so this
    /// is operational attribution (resolved server-side to a display name for
    /// TravelRequest.CreatedBy), never authentication. The frontend must never send a
    /// display name directly.
    /// </summary>
    public int? UserId { get; set; }

    // --- Travellers ---
    /// <summary>
    /// One object per frontend traveller row; name, employee/person id, department and
    /// contact information stay grouped per traveller. Null or empty list is accepted
    /// (frontend-owned requiredness). Rows with all four values blank are ignored.
    /// </summary>
    public List<TravelTravellerDto>? Travellers { get; set; }

    // --- Trip ---
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

    // --- Transportation ---
    public string? TransportType { get; set; }
    public DateTime? PreferredDeparture { get; set; }
    public DateTime? PreferredArrival { get; set; }
    public string? ClassPreference { get; set; }
    public string? BookingRequirements { get; set; }

    // --- Hotel ---
    public string? Hotel { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public int? NumberOfRooms { get; set; }
    public string? RoomPreference { get; set; }
    public string? LocationPreference { get; set; }

    // --- Local Transport ---
    public bool? PickupRequired { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropLocation { get; set; }
    public string? VehiclePreference { get; set; }

    // --- Hospitality ---
    public string? ClientGuestDetails { get; set; }
    public string? HospitalityRequirement { get; set; }
    public string? MeetingEventPurpose { get; set; }
    public int? NumberOfGuests { get; set; }
    public string? SpecialArrangements { get; set; }

    // --- Itinerary ---
    public string? ItineraryNotes { get; set; }
    public string? AdditionalInstructions { get; set; }

    // --- Budget ---
    public decimal? EstimatedTravelCost { get; set; }
    public decimal? EstimatedHotelCost { get; set; }
    public decimal? EstimatedLocalTransportCost { get; set; }
    public decimal? EstimatedHospitalityCost { get; set; }
    public string? Currency { get; set; }

    // --- Approval routing ---
    public bool ApprovalRequired { get; set; }
    /// <summary>
    /// Opaque approver identifier sourced from the authenticated identity system.
    /// ApproverNameSnapshot is backend-owned; optional identity lookup is deferred.
    /// </summary>
    public string? ApproverId { get; set; }
    /// <summary>Who actually approved (business data; not the designated approver and not audit attribution). The approve action sets it authoritatively.</summary>
    public string? ApprovedBy { get; set; }
    /// <summary>Who actually rejected (business data). The reject action sets it authoritatively.</summary>
    public string? RejectedBy { get; set; }
}

// ============================================================
// UPDATE DTO
// ============================================================

/// <summary>
/// Fields the frontend may supply when updating a Travel Draft.
/// Immutable fields (Id, ReferenceNo, EaTaskId, CurrentCycleNo, CreatedBy,
/// CreatedDate, SubmittedAt, ApprovedAt, RejectedAt, CompletedAt, IsDeleted)
/// are NOT present here.
/// </summary>
public class UpdateTravelDraftDto
{
    // --- Travellers ---
    /// <summary>
    /// Full replacement of the traveller rows; same structure as on create.
    /// Null or empty list is accepted (frontend-owned requiredness).
    /// </summary>
    public List<TravelTravellerDto>? Travellers { get; set; }

    // --- Trip ---
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

    // --- Transportation ---
    public string? TransportType { get; set; }
    public DateTime? PreferredDeparture { get; set; }
    public DateTime? PreferredArrival { get; set; }
    public string? ClassPreference { get; set; }
    public string? BookingRequirements { get; set; }

    // --- Hotel ---
    public string? Hotel { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public int? NumberOfRooms { get; set; }
    public string? RoomPreference { get; set; }
    public string? LocationPreference { get; set; }

    // --- Local Transport ---
    public bool? PickupRequired { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropLocation { get; set; }
    public string? VehiclePreference { get; set; }

    // --- Hospitality ---
    public string? ClientGuestDetails { get; set; }
    public string? HospitalityRequirement { get; set; }
    public string? MeetingEventPurpose { get; set; }
    public int? NumberOfGuests { get; set; }
    public string? SpecialArrangements { get; set; }

    // --- Itinerary ---
    public string? ItineraryNotes { get; set; }
    public string? AdditionalInstructions { get; set; }

    // --- Budget ---
    public decimal? EstimatedTravelCost { get; set; }
    public decimal? EstimatedHotelCost { get; set; }
    public decimal? EstimatedLocalTransportCost { get; set; }
    public decimal? EstimatedHospitalityCost { get; set; }
    public string? Currency { get; set; }

    // --- Approval routing ---
    public bool ApprovalRequired { get; set; }
    public string? ApproverId { get; set; }
    /// <summary>Who actually approved (business data; not the designated approver and not audit attribution). The approve action sets it authoritatively.</summary>
    public string? ApprovedBy { get; set; }
    /// <summary>Who actually rejected (business data). The reject action sets it authoritatively.</summary>
    public string? RejectedBy { get; set; }
}

// ============================================================
// RESPONSE SUB-OBJECTS
// ============================================================

/// <summary>One traveller row — used unchanged for create, update and read.</summary>
public class TravelTravellerDto
{
    public string? TravellerName { get; set; }
    public string? EmployeePersonId { get; set; }
    public string? Department { get; set; }
    public string? ContactInformation { get; set; }
}

public class TravelTripDto
{
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
}

public class TravelTransportationDto
{
    public string? TransportType { get; set; }
    public DateTime? PreferredDeparture { get; set; }
    public DateTime? PreferredArrival { get; set; }
    public string? ClassPreference { get; set; }
    public string? BookingRequirements { get; set; }
}

public class TravelHotelDto
{
    public string? Hotel { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public int? NumberOfRooms { get; set; }
    public string? RoomPreference { get; set; }
    public string? LocationPreference { get; set; }
}

public class TravelLocalTransportDto
{
    public bool? PickupRequired { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropLocation { get; set; }
    public string? VehiclePreference { get; set; }
}

public class TravelHospitalityDto
{
    public string? ClientGuestDetails { get; set; }
    public string? HospitalityRequirement { get; set; }
    public string? MeetingEventPurpose { get; set; }
    public int? NumberOfGuests { get; set; }
    public string? SpecialArrangements { get; set; }
}

public class TravelItineraryDto
{
    public string? ItineraryNotes { get; set; }
    public string? AdditionalInstructions { get; set; }
}

public class TravelBudgetDto
{
    public decimal? EstimatedTravelCost { get; set; }
    public decimal? EstimatedHotelCost { get; set; }
    public decimal? EstimatedLocalTransportCost { get; set; }
    public decimal? EstimatedHospitalityCost { get; set; }
    /// <summary>
    /// Server-calculated sum of supplied component estimates.
    /// Null components are treated as zero for calculation only;
    /// stored null values are not modified.
    /// </summary>
    public decimal TotalEstimatedCost { get; set; }
    public string? Currency { get; set; }
}

public class TravelApprovalDto
{
    public bool Required { get; set; }
    public string? ApproverId { get; set; }
    /// <summary>Resolved from identity system at creation time; not writable by frontend.</summary>
    public string? ApproverName { get; set; }
    public string? State { get; set; }
    /// <summary>Who actually approved (not the designated approver).</summary>
    public string? ApprovedBy { get; set; }
    /// <summary>Who actually rejected.</summary>
    public string? RejectedBy { get; set; }
}

// ============================================================
// DETAIL RESPONSE
// ============================================================

public class TravelRequestDetailDto
{
    public TravelCurrentCycleDto? CurrentCycle { get; set; }
    public long Id { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public long EaTaskId { get; set; }
    public int CurrentCycleNo { get; set; }
    public string BusinessState { get; set; } = string.Empty;
    public string ApprovalState { get; set; } = string.Empty;

    public List<TravelTravellerDto> Travellers { get; set; } = new();
    public TravelTripDto Trip { get; set; } = new();
    public TravelTransportationDto Transportation { get; set; } = new();
    public TravelHotelDto Hotel { get; set; } = new();
    public TravelLocalTransportDto LocalTransport { get; set; } = new();
    public TravelHospitalityDto Hospitality { get; set; } = new();
    public TravelItineraryDto Itinerary { get; set; } = new();
    public TravelBudgetDto Budget { get; set; } = new();
    public TravelApprovalDto Approval { get; set; } = new();

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

// ============================================================
// CREATE RESPONSE
// ============================================================

public class TravelRequestCreatedDto
{
    public long TravelRequestId { get; set; }
    /// <summary>Id of the central EaTask created atomically with this TravelRequest.</summary>
    public long EaTaskId { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string BusinessState { get; set; } = string.Empty;
    public string ApprovalState { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string? RejectedBy { get; set; }
}

// ============================================================
// LIST ITEM DTO (lightweight)
// ============================================================

public class TravelRequestListItemDto
{
    public int CurrentCycleNo { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? RejectedBy { get; set; }
    public long Id { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public List<TravelTravellerDto> Travellers { get; set; } = new();
    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }
    public string? Purpose { get; set; }
    public DateTime? DepartureDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public string BusinessState { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public string ApprovalState { get; set; } = string.Empty;
    public decimal TotalEstimatedCost { get; set; }
    public string? Currency { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    // TRAVEL FOLLOW-UP SOURCE INTEGRATION DEFERRED
    public DateTime? FollowUpDate { get; set; }
    public string? FollowUpStatus { get; set; }
}

// ============================================================
// LIST QUERY / FILTER
// ============================================================

public class TravelRequestListQueryDto
{
    /// <summary>Full-text search across ReferenceNo, Travellers[*].TravellerName, FromLocation, ToLocation, Purpose.</summary>
    public string? Search { get; set; }

    // Explicit state filters (do not use a single ambiguous "status")
    public string? BusinessState { get; set; }
    public string? ApprovalState { get; set; }

    public string? Priority { get; set; }
    /// <summary>Filter by traveller name — matches if any traveller's TravellerName contains this substring (case-insensitive).</summary>
    public string? TravellerName { get; set; }
    public string? Department { get; set; }
    public string? CreatedBy { get; set; }
    public string? ApproverId { get; set; }

    /// <summary>
    /// Date-only filter applied as day-boundary range (00:00:00Z to 23:59:59Z) for RequiredDate.
    /// </summary>
    public DateTime? RequiredDateFrom { get; set; }
    public DateTime? RequiredDateTo { get; set; }

    /// <summary>
    /// Date-only filter applied as day-boundary range for DepartureDate.
    /// </summary>
    public DateTime? DepartureDateFrom { get; set; }
    public DateTime? DepartureDateTo { get; set; }

    public string? ReferenceNo { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
