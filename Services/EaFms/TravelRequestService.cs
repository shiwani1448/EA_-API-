using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Travel Request CRUD/read service (Step 3).
///
/// POST (create draft) is PARTIALLY BLOCKED:
///   ⚠ BLOCKED — TRAVEL EATASK/TAT CREATION POLICY REQUIRES DECISION
///   The EaTaskService enforces that only EA Approval may skip TAT.
///   Travel has no TAT rule and is not EA Approval, so no valid EaTask
///   creation path currently exists.
///   ReferenceNo is generated and the TravelRequest is persisted; EaTaskId
///   is set to 0 as a placeholder. The FK constraint must be relaxed or the
///   TAT/task policy must be resolved before true production use.
///
///   TRAVEL BUSINESS MODULE CONFIGURATION REQUIRED BEFORE RUNTIME TRAVEL CREATION
///   The service validates that a "Travel" BusinessModule record is present and
///   active before attempting any creation.
/// </summary>
public class TravelRequestService : ITravelRequestService
{
    private readonly EaFmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public TravelRequestService(EaFmsDbContext db, IAuditService audit, ICurrentUserService currentUser)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    // ============================================================
    // CREATE DRAFT
    // ============================================================

    public async Task<TravelRequestCreatedDto> CreateDraftAsync(CreateTravelRequestDto dto, CancellationToken ct = default)
    {
        // ---- 1. Resolve Travel BusinessModule ----
        // Dynamic lookup; never hardcode a module ID.
        var travelModule = await _db.BusinessModules
            .Where(m => !m.IsDeleted && m.IsActive
                && (m.Name.Trim().ToLower() == "travel" || m.Name.Trim().ToLower() == "ea travel"))
            .FirstOrDefaultAsync(ct);

        if (travelModule is null)
        {
            // TRAVEL BUSINESS MODULE CONFIGURATION REQUIRED BEFORE RUNTIME TRAVEL CREATION
            throw new BusinessRuleException(
                "TRAVEL BUSINESS MODULE CONFIGURATION REQUIRED BEFORE RUNTIME TRAVEL CREATION. " +
                "Insert an active 'Travel' or 'EA Travel' record into ea_business_modules.");
        }

        // ---- 2. Generate reference number (ea_travel_no_seq) ----
        var seqValue = await _db.Database
            .SqlQueryRaw<long>("SELECT nextval('ea_travel_no_seq') AS \"Value\"")
            .SingleAsync(ct);

        var year = DateTime.UtcNow.Year;
        var referenceNo = $"TRV-{year}-{seqValue:D6}";

        var now = Clock.UtcNowTz;
        var actor = _currentUser.UserName ?? _currentUser.UserId.ToString(CultureInfo.InvariantCulture);

        // ---- 3. Determine initial ApprovalState ----
        var approvalState = dto.ApprovalRequired ? "Pending" : "NotRequired";

        // ---- 4. Build entity ----
        // BLOCKED: EaTaskId = 0 is a placeholder because TravelRequest.EaTaskId is
        // a non-nullable FK and no valid EaTask creation path currently exists for Travel.
        // This will violate the FK constraint in the real PostgreSQL database.
        // The EaTask must be created once the TAT/task-creation policy is approved.
        var entity = new TravelRequest
        {
            ReferenceNo = referenceNo,
            EaTaskId = 0, // ⚠ PLACEHOLDER — FK NOT SATISFIED until TAT policy resolved
            CurrentCycleNo = 0,
            TravellerName = dto.TravellerName?.Trim(),
            EmployeePersonId = dto.EmployeePersonId?.Trim(),
            Department = dto.Department?.Trim(),
            ContactInformation = dto.ContactInformation?.Trim(),
            Purpose = dto.Purpose?.Trim(),
            TravelType = dto.TravelType?.Trim(),
            FromLocation = dto.FromLocation?.Trim(),
            ToLocation = dto.ToLocation?.Trim(),
            DepartureDate = dto.DepartureDate,
            ReturnDate = dto.ReturnDate,
            NumberOfTravellers = dto.NumberOfTravellers,
            Priority = dto.Priority?.Trim(),
            SpecialRequirements = dto.SpecialRequirements?.Trim(),
            RequiredDate = dto.RequiredDate,
            TransportType = dto.TransportType?.Trim(),
            PreferredDeparture = dto.PreferredDeparture,
            PreferredArrival = dto.PreferredArrival,
            ClassPreference = dto.ClassPreference?.Trim(),
            BookingRequirements = dto.BookingRequirements?.Trim(),
            Hotel = dto.Hotel?.Trim(),
            CheckInDate = dto.CheckInDate,
            CheckOutDate = dto.CheckOutDate,
            NumberOfRooms = dto.NumberOfRooms,
            RoomPreference = dto.RoomPreference?.Trim(),
            LocationPreference = dto.LocationPreference?.Trim(),
            PickupRequired = dto.PickupRequired,
            PickupLocation = dto.PickupLocation?.Trim(),
            DropLocation = dto.DropLocation?.Trim(),
            VehiclePreference = dto.VehiclePreference?.Trim(),
            ClientGuestDetails = dto.ClientGuestDetails?.Trim(),
            HospitalityRequirement = dto.HospitalityRequirement?.Trim(),
            MeetingEventPurpose = dto.MeetingEventPurpose?.Trim(),
            NumberOfGuests = dto.NumberOfGuests,
            SpecialArrangements = dto.SpecialArrangements?.Trim(),
            ItineraryNotes = dto.ItineraryNotes?.Trim(),
            AdditionalInstructions = dto.AdditionalInstructions?.Trim(),
            EstimatedTravelCost = dto.EstimatedTravelCost,
            EstimatedHotelCost = dto.EstimatedHotelCost,
            EstimatedLocalTransportCost = dto.EstimatedLocalTransportCost,
            EstimatedHospitalityCost = dto.EstimatedHospitalityCost,
            Currency = dto.Currency?.Trim(),
            ApprovalRequired = dto.ApprovalRequired,
            ApproverId = dto.ApproverId?.Trim(),
            // ApproverNameSnapshot: resolved server-side; not accepted from frontend.
            // Current architecture does not expose an HRMS person-lookup service here.
            // Snapshot must be populated once HRMS identity lookup is wired in.
            ApproverNameSnapshot = null,
            BusinessState = "Draft",
            ApprovalState = approvalState,
            CreatedBy = actor,
            CreatedDate = now,
            IsDeleted = false
        };

        // ---- 5. Persist (BLOCKED at FK level until EaTask exists) ----
        // Note: In the current state, saving will fail with a FK violation because
        // EaTaskId = 0 does not reference a valid ea_tasks row.
        // The service layer is complete; the blocker is architectural (TAT policy).
        await _db.TravelRequests.AddAsync(entity, ct);

        // Audit (before SaveChanges — follows EA pattern)
        _audit.AddAudit(
            "TRAVEL_CREATE_DRAFT",
            "Travel",
            nameof(TravelRequest),
            "pending", // entity.Id not yet assigned
            null,
            new { entity.ReferenceNo, entity.BusinessState, entity.ApprovalState, travelModule.Id },
            "Travel request draft created");

        await _db.SaveChangesAsync(ct);

        // Update audit with real Id now that SaveChanges assigned it
        _audit.AddAudit(
            "TRAVEL_CREATE_DRAFT_ID_ASSIGNED",
            "Travel",
            nameof(TravelRequest),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            null,
            new { entity.Id, entity.ReferenceNo },
            "Travel request id assigned after insert");

        await _db.SaveChangesAsync(ct);

        return new TravelRequestCreatedDto
        {
            TravelRequestId = entity.Id,
            EaTaskId = entity.EaTaskId, // will be 0 until TAT policy resolved
            ReferenceNo = entity.ReferenceNo,
            BusinessState = entity.BusinessState,
            ApprovalState = entity.ApprovalState
        };
    }

    // ============================================================
    // GET DETAIL
    // ============================================================

    public async Task<TravelRequestDetailDto> GetByIdAsync(long travelRequestId, CancellationToken ct = default)
    {
        var entity = await _db.TravelRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == travelRequestId && !x.IsDeleted, ct)
            ?? throw new NotFoundException($"Travel request {travelRequestId} not found.");

        return ToDetailDto(entity);
    }

    // ============================================================
    // UPDATE DRAFT
    // ============================================================

    public async Task<TravelRequestDetailDto> UpdateDraftAsync(
        long travelRequestId, UpdateTravelDraftDto dto, CancellationToken ct = default)
    {
        var entity = await _db.TravelRequests
            .FirstOrDefaultAsync(x => x.Id == travelRequestId && !x.IsDeleted, ct)
            ?? throw new NotFoundException($"Travel request {travelRequestId} not found.");

        // Editability gate: only Draft is editable at this step.
        // Post-submission edit semantics (ChangesRequested rework) belong to Step 4+.
        if (!string.Equals(entity.BusinessState, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                $"Travel request is in state '{entity.BusinessState}' and cannot be edited via the draft endpoint. " +
                "Only Draft requests may be updated here.");
        }

        // Snapshot for audit
        var snapshot = new
        {
            entity.TravellerName, entity.Purpose, entity.FromLocation, entity.ToLocation,
            entity.DepartureDate, entity.ReturnDate, entity.Priority, entity.BusinessState,
            entity.ApprovalState, entity.ApprovalRequired, entity.ApproverId
        };

        // Apply all frontend-editable fields
        entity.TravellerName = dto.TravellerName?.Trim();
        entity.EmployeePersonId = dto.EmployeePersonId?.Trim();
        entity.Department = dto.Department?.Trim();
        entity.ContactInformation = dto.ContactInformation?.Trim();
        entity.Purpose = dto.Purpose?.Trim();
        entity.TravelType = dto.TravelType?.Trim();
        entity.FromLocation = dto.FromLocation?.Trim();
        entity.ToLocation = dto.ToLocation?.Trim();
        entity.DepartureDate = dto.DepartureDate;
        entity.ReturnDate = dto.ReturnDate;
        entity.NumberOfTravellers = dto.NumberOfTravellers;
        entity.Priority = dto.Priority?.Trim();
        entity.SpecialRequirements = dto.SpecialRequirements?.Trim();
        entity.RequiredDate = dto.RequiredDate;
        entity.TransportType = dto.TransportType?.Trim();
        entity.PreferredDeparture = dto.PreferredDeparture;
        entity.PreferredArrival = dto.PreferredArrival;
        entity.ClassPreference = dto.ClassPreference?.Trim();
        entity.BookingRequirements = dto.BookingRequirements?.Trim();
        entity.Hotel = dto.Hotel?.Trim();
        entity.CheckInDate = dto.CheckInDate;
        entity.CheckOutDate = dto.CheckOutDate;
        entity.NumberOfRooms = dto.NumberOfRooms;
        entity.RoomPreference = dto.RoomPreference?.Trim();
        entity.LocationPreference = dto.LocationPreference?.Trim();
        entity.PickupRequired = dto.PickupRequired;
        entity.PickupLocation = dto.PickupLocation?.Trim();
        entity.DropLocation = dto.DropLocation?.Trim();
        entity.VehiclePreference = dto.VehiclePreference?.Trim();
        entity.ClientGuestDetails = dto.ClientGuestDetails?.Trim();
        entity.HospitalityRequirement = dto.HospitalityRequirement?.Trim();
        entity.MeetingEventPurpose = dto.MeetingEventPurpose?.Trim();
        entity.NumberOfGuests = dto.NumberOfGuests;
        entity.SpecialArrangements = dto.SpecialArrangements?.Trim();
        entity.ItineraryNotes = dto.ItineraryNotes?.Trim();
        entity.AdditionalInstructions = dto.AdditionalInstructions?.Trim();
        entity.EstimatedTravelCost = dto.EstimatedTravelCost;
        entity.EstimatedHotelCost = dto.EstimatedHotelCost;
        entity.EstimatedLocalTransportCost = dto.EstimatedLocalTransportCost;
        entity.EstimatedHospitalityCost = dto.EstimatedHospitalityCost;
        entity.Currency = dto.Currency?.Trim();
        entity.ApprovalRequired = dto.ApprovalRequired;
        entity.ApproverId = dto.ApproverId?.Trim();
        // ApproverNameSnapshot: keep existing value or clear if ApproverId changed.
        // Full HRMS lookup deferred; null out snapshot on approver change for consistency.
        if (!string.Equals(entity.ApproverId, dto.ApproverId?.Trim(), StringComparison.Ordinal))
            entity.ApproverNameSnapshot = null;

        // Update ApprovalState if ApprovalRequired flag changed
        if (!dto.ApprovalRequired)
            entity.ApprovalState = "NotRequired";
        else if (string.Equals(entity.ApprovalState, "NotRequired", StringComparison.OrdinalIgnoreCase))
            entity.ApprovalState = "Pending";

        // Backend-owned fields: do NOT allow frontend to set these
        // entity.Id, entity.ReferenceNo, entity.EaTaskId, entity.CurrentCycleNo,
        // entity.BusinessState (not mutated here), entity.CreatedBy, entity.CreatedDate,
        // entity.SubmittedAt, entity.ApprovedAt, entity.RejectedAt, entity.CompletedAt

        // Set audit trail from server context
        entity.ModifiedBy = _currentUser.UserName ?? _currentUser.UserId.ToString(CultureInfo.InvariantCulture);
        entity.ModifiedDate = Clock.UtcNowTz;

        _audit.AddAudit(
            "TRAVEL_UPDATE_DRAFT",
            "Travel",
            nameof(TravelRequest),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            snapshot,
            new { entity.TravellerName, entity.Purpose, entity.FromLocation, entity.ToLocation,
                  entity.DepartureDate, entity.ReturnDate, entity.Priority,
                  entity.ApprovalRequired, entity.ApproverId },
            "Travel request draft updated");

        await _db.SaveChangesAsync(ct);

        return ToDetailDto(entity);
    }

    // ============================================================
    // LIST / SEARCH / FILTER
    // ============================================================

    public async Task<PagedResult<TravelRequestListItemDto>> ListAsync(
        TravelRequestListQueryDto query, CancellationToken ct = default)
    {
        var q = _db.TravelRequests
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        // ---- Explicit state filters ----
        if (!string.IsNullOrWhiteSpace(query.BusinessState))
        {
            var bs = query.BusinessState.Trim();
            q = q.Where(x => x.BusinessState == bs);
        }

        if (!string.IsNullOrWhiteSpace(query.ApprovalState))
        {
            var aps = query.ApprovalState.Trim();
            q = q.Where(x => x.ApprovalState == aps);
        }

        // ---- Field filters ----
        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            var pri = query.Priority.Trim().ToLower();
            q = q.Where(x => x.Priority != null && x.Priority.Trim().ToLower() == pri);
        }

        if (!string.IsNullOrWhiteSpace(query.TravellerName))
        {
            var tn = query.TravellerName.Trim().ToLower();
            q = q.Where(x => x.TravellerName != null && x.TravellerName.ToLower().Contains(tn));
        }

        if (!string.IsNullOrWhiteSpace(query.Department))
        {
            var dept = query.Department.Trim().ToLower();
            q = q.Where(x => x.Department != null && x.Department.ToLower().Contains(dept));
        }

        if (!string.IsNullOrWhiteSpace(query.CreatedBy))
        {
            var cb = query.CreatedBy.Trim().ToLower();
            q = q.Where(x => x.CreatedBy.ToLower().Contains(cb));
        }

        if (!string.IsNullOrWhiteSpace(query.ApproverId))
        {
            var aid = query.ApproverId.Trim();
            q = q.Where(x => x.ApproverId == aid);
        }

        if (!string.IsNullOrWhiteSpace(query.ReferenceNo))
        {
            var refNo = query.ReferenceNo.Trim().ToLower();
            q = q.Where(x => x.ReferenceNo.ToLower().Contains(refNo));
        }

        // ---- Date-range filters (day boundary semantics) ----
        // Date-only filters are treated as inclusive day ranges (00:00:00Z to end-of-day)
        // consistent with the existing EA project convention.

        if (query.RequiredDateFrom.HasValue)
        {
            var from = query.RequiredDateFrom.Value.Date.ToUniversalTime();
            q = q.Where(x => x.RequiredDate.HasValue && x.RequiredDate >= from);
        }
        if (query.RequiredDateTo.HasValue)
        {
            var to = query.RequiredDateTo.Value.Date.AddDays(1).ToUniversalTime();
            q = q.Where(x => x.RequiredDate.HasValue && x.RequiredDate < to);
        }

        if (query.DepartureDateFrom.HasValue)
        {
            var from = query.DepartureDateFrom.Value.Date.ToUniversalTime();
            q = q.Where(x => x.DepartureDate.HasValue && x.DepartureDate >= from);
        }
        if (query.DepartureDateTo.HasValue)
        {
            var to = query.DepartureDateTo.Value.Date.AddDays(1).ToUniversalTime();
            q = q.Where(x => x.DepartureDate.HasValue && x.DepartureDate < to);
        }

        // ---- Search (case-insensitive, multi-field) ----
        // Matches ReferenceNo, TravellerName, FromLocation, ToLocation, Purpose.
        // Uses EF-compatible ToLower(); does not require full-text infrastructure.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(x =>
                x.ReferenceNo.ToLower().Contains(term) ||
                (x.TravellerName != null && x.TravellerName.ToLower().Contains(term)) ||
                (x.FromLocation != null && x.FromLocation.ToLower().Contains(term)) ||
                (x.ToLocation != null && x.ToLocation.ToLower().Contains(term)) ||
                (x.Purpose != null && x.Purpose.ToLower().Contains(term)));
        }

        // ---- Pagination ----
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : query.PageSize > 200 ? 200 : query.PageSize;

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TravelRequestListItemDto>
        {
            Items = items.Select(ToListItemDto).ToArray(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // ============================================================
    // MAPPING HELPERS (manual — no AutoMapper dependency needed here)
    // ============================================================

    private static TravelRequestDetailDto ToDetailDto(TravelRequest e)
    {
        return new TravelRequestDetailDto
        {
            Id = e.Id,
            ReferenceNo = e.ReferenceNo,
            EaTaskId = e.EaTaskId,
            CurrentCycleNo = e.CurrentCycleNo,
            BusinessState = e.BusinessState,
            ApprovalState = e.ApprovalState,

            Traveller = new TravelTravellerDto
            {
                TravellerName = e.TravellerName,
                EmployeePersonId = e.EmployeePersonId,
                Department = e.Department,
                ContactInformation = e.ContactInformation
            },

            Trip = new TravelTripDto
            {
                Purpose = e.Purpose,
                TravelType = e.TravelType,
                FromLocation = e.FromLocation,
                ToLocation = e.ToLocation,
                DepartureDate = e.DepartureDate,
                ReturnDate = e.ReturnDate,
                NumberOfTravellers = e.NumberOfTravellers,
                Priority = e.Priority,
                SpecialRequirements = e.SpecialRequirements,
                RequiredDate = e.RequiredDate
            },

            Transportation = new TravelTransportationDto
            {
                TransportType = e.TransportType,
                PreferredDeparture = e.PreferredDeparture,
                PreferredArrival = e.PreferredArrival,
                ClassPreference = e.ClassPreference,
                BookingRequirements = e.BookingRequirements
            },

            Hotel = new TravelHotelDto
            {
                Hotel = e.Hotel,
                CheckInDate = e.CheckInDate,
                CheckOutDate = e.CheckOutDate,
                NumberOfRooms = e.NumberOfRooms,
                RoomPreference = e.RoomPreference,
                LocationPreference = e.LocationPreference
            },

            LocalTransport = new TravelLocalTransportDto
            {
                PickupRequired = e.PickupRequired,
                PickupLocation = e.PickupLocation,
                DropLocation = e.DropLocation,
                VehiclePreference = e.VehiclePreference
            },

            Hospitality = new TravelHospitalityDto
            {
                ClientGuestDetails = e.ClientGuestDetails,
                HospitalityRequirement = e.HospitalityRequirement,
                MeetingEventPurpose = e.MeetingEventPurpose,
                NumberOfGuests = e.NumberOfGuests,
                SpecialArrangements = e.SpecialArrangements
            },

            Itinerary = new TravelItineraryDto
            {
                ItineraryNotes = e.ItineraryNotes,
                AdditionalInstructions = e.AdditionalInstructions
            },

            Budget = new TravelBudgetDto
            {
                EstimatedTravelCost = e.EstimatedTravelCost,
                EstimatedHotelCost = e.EstimatedHotelCost,
                EstimatedLocalTransportCost = e.EstimatedLocalTransportCost,
                EstimatedHospitalityCost = e.EstimatedHospitalityCost,
                TotalEstimatedCost = CalculateTotalEstimatedCost(e),
                Currency = e.Currency
            },

            Approval = new TravelApprovalDto
            {
                Required = e.ApprovalRequired,
                ApproverId = e.ApproverId,
                ApproverName = e.ApproverNameSnapshot,
                State = e.ApprovalState
            },

            SubmittedAt = e.SubmittedAt,
            ApprovedAt = e.ApprovedAt,
            RejectedAt = e.RejectedAt,
            CompletedAt = e.CompletedAt,
            CreatedBy = e.CreatedBy,
            CreatedDate = e.CreatedDate,
            ModifiedBy = e.ModifiedBy,
            ModifiedDate = e.ModifiedDate
        };
    }

    private static TravelRequestListItemDto ToListItemDto(TravelRequest e)
    {
        return new TravelRequestListItemDto
        {
            Id = e.Id,
            ReferenceNo = e.ReferenceNo,
            TravellerName = e.TravellerName,
            EmployeePersonId = e.EmployeePersonId,
            Department = e.Department,
            FromLocation = e.FromLocation,
            ToLocation = e.ToLocation,
            Purpose = e.Purpose,
            DepartureDate = e.DepartureDate,
            ReturnDate = e.ReturnDate,
            BusinessState = e.BusinessState,
            Priority = e.Priority,
            ApprovalState = e.ApprovalState,
            TotalEstimatedCost = CalculateTotalEstimatedCost(e),
            Currency = e.Currency,
            CreatedBy = e.CreatedBy,
            CreatedDate = e.CreatedDate,
            ModifiedBy = e.ModifiedBy,
            ModifiedDate = e.ModifiedDate,
            // TRAVEL FOLLOW-UP SOURCE INTEGRATION DEFERRED
            FollowUpDate = null,
            FollowUpStatus = null
        };
    }

    /// <summary>
    /// Server-side TotalEstimatedCost calculation.
    /// Null components are treated as zero for display purposes only.
    /// Stored null values are NOT mutated; this is a projection-only calculation.
    /// Public for unit-test access.
    /// </summary>
    public static decimal CalculateTotalEstimatedCost(TravelRequest e)
    {
        return (e.EstimatedTravelCost ?? 0m)
             + (e.EstimatedHotelCost ?? 0m)
             + (e.EstimatedLocalTransportCost ?? 0m)
             + (e.EstimatedHospitalityCost ?? 0m);
    }
}
