using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Repositories.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>
/// Travel CRUD and submission/approval service.
///
/// Create draft persists a TravelRequest and its required central EaTask atomically.
/// Travel has no approved TAT classification yet, so the EaTask is created via the
/// backend-only no-TAT path (EaTaskService.CreateWithoutTatAsync) with
/// AllottedTatMinutes = NULL, module policy resolved by module name (never by a
/// frontend-supplied flag). Module lookup uses the canonical catalog name
/// "Travel &amp; Hospitality".
/// </summary>
public partial class TravelRequestService : ITravelRequestService
{
    public const string TravelBusinessModuleName = "Travel & Hospitality";

    private readonly EaFmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;
    private readonly ITravelNumberRepository _travelNumbers;
    private readonly IEaTaskService _eaTaskService;

    public TravelRequestService(
        EaFmsDbContext db,
        IAuditService audit,
        ICurrentUserService currentUser,
        ITravelNumberRepository travelNumbers,
        IEaTaskService eaTaskService)
    {
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
        _travelNumbers = travelNumbers;
        _eaTaskService = eaTaskService;
    }

    // ============================================================
    // CREATE DRAFT
    // ============================================================

    public async Task<TravelRequestCreatedDto> CreateDraftAsync(CreateTravelRequestDto dto, CancellationToken ct = default)
    {
        // ---- 1. Resolve Travel BusinessModule (canonical name; never hardcode IDs) ----
        var travelModule = await _db.BusinessModules
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.IsActive && m.Name == TravelBusinessModuleName)
            .FirstOrDefaultAsync(ct);

        if (travelModule is null)
        {
            throw new BusinessRuleException(
                "TRAVEL BUSINESS MODULE CONFIGURATION REQUIRED BEFORE RUNTIME TRAVEL CREATION. " +
                $"Insert an active '{TravelBusinessModuleName}' record into ea_business_modules.");
        }

        var actor = _currentUser.UserName ?? _currentUser.UserId.ToString(CultureInfo.InvariantCulture);
        if (_currentUser.UserId == 0 && _currentUser.UserName is null)
            throw new BusinessRuleException("Authenticated user identity is required to create a Travel request.");
        var now = Clock.UtcNowTz;
        var referenceNo = await _travelNumbers.GenerateNextReferenceNoAsync(ct);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // ---- 2. Create the required central EaTask before the TravelRequest ----
        // TravelRequest.EaTaskId is a required, non-deferrable FK, so a valid EaTask must
        // exist before TravelRequest can be inserted. TravelRequest.Id is not known yet
        // (identity-generated on insert), so BusinessRecordId is seeded with ReferenceNo
        // here and corrected to the real TravelRequest.Id below, before commit — the
        // persisted row never keeps the placeholder value. No EaTaskId=0 is ever set.
        // Travel has no approved TAT classification, so this always goes through the
        // backend-only no-TAT path — never TAT-required, never a frontend-selectable flag.
        var eaTaskDto = await _eaTaskService.CreateWithoutTatAsync(new CreateEaTaskDto
        {
            ModuleId = travelModule.Id,
            BusinessRecordId = referenceNo,
            Task = referenceNo,
            Description = string.IsNullOrWhiteSpace(dto.Purpose) ? null : dto.Purpose.Trim(),
            WorkflowInstanceId = null
        }, ct);

        var entity = new TravelRequest
        {
            ReferenceNo = referenceNo,
            EaTaskId = eaTaskDto.EaTaskId,
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
            BusinessState = "Draft",
            ApprovalState = ResolveDraftApprovalState(dto.ApprovalRequired),

            CreatedBy = actor,
            CreatedDate = now
        };

        _db.TravelRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        // ---- 3. Correct the EaTask's BusinessRecordId to the real TravelRequest.Id ----
        // Now that it exists. No orphan can result: if anything above or below fails,
        // the whole transaction — including the EaTask insert — rolls back.
        var eaTask = await _db.Tasks.FirstAsync(t => t.Id == eaTaskDto.EaTaskId, ct);
        eaTask.BusinessRecordId = entity.Id.ToString(CultureInfo.InvariantCulture);
        await _db.SaveChangesAsync(ct);

        _audit.AddAudit(
            "TRAVEL_CREATE_DRAFT",
            "Travel",
            nameof(TravelRequest),
            entity.Id.ToString(CultureInfo.InvariantCulture),
            null,
            new { entity.ReferenceNo, entity.EaTaskId, entity.BusinessState, entity.ApprovalState },
            "Travel request draft created");
        await _db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return new TravelRequestCreatedDto
        {
            TravelRequestId = entity.Id,
            EaTaskId = entity.EaTaskId,
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

        var cycle = await _db.TravelRequestCycles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TravelRequestId == entity.Id && x.CycleNo == entity.CurrentCycleNo, ct);
        return ToDetailDto(entity, cycle);
    }

    // ============================================================
    // UPDATE DRAFT
    // ============================================================

    public async Task<TravelRequestDetailDto> UpdateDraftAsync(
        long travelRequestId, UpdateTravelDraftDto dto, CancellationToken ct = default, int? expectedCycleNo = null)
    {
        if (expectedCycleNo.HasValue && expectedCycleNo <= 0)
            throw new BadRequestException("ExpectedCycleNo must be greater than zero.");
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var entity = await LockTravelParentAsync(travelRequestId, ct);
        var rework = entity.ApprovalState == "ChangesRequested";
        TravelRequestCycle? currentCycle = null;
        if (rework)
        {
            if (!expectedCycleNo.HasValue)
                throw new BusinessRuleException("ExpectedCycleNo is required for controlled rework.");
            currentCycle = await LockCurrentTravelCycleAsync(entity, expectedCycleNo.Value, "ChangesRequested", ct);
            if (dto.ApprovalRequired != entity.ApprovalRequired ||
                !string.Equals(dto.ApproverId?.Trim(), entity.ApproverId, StringComparison.Ordinal))
                throw new BusinessRuleException("Approval routing cannot change during rework.");
        }
        else
        {
            await ValidateUnsubmittedTravelAsync(entity, ct);
            if (expectedCycleNo.HasValue)
                throw new BusinessRuleException("An unsubmitted draft has no approval cycle.");
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
        if (!rework)
        {
            // Opaque IDs remain opaque; optional HRMS snapshot resolution is deferred.
            if (!string.Equals(entity.ApproverId, dto.ApproverId?.Trim(), StringComparison.Ordinal))
                entity.ApproverNameSnapshot = null;
            entity.ApprovalRequired = dto.ApprovalRequired;
            entity.ApproverId = dto.ApproverId?.Trim();
            entity.ApprovalState = ResolveDraftApprovalState(dto.ApprovalRequired);
        }

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
                  entity.ApprovalRequired, entity.ApproverId, entity.ApprovalState },
            "Travel request draft updated");

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return ToDetailDto(entity, currentCycle);
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

    /// <summary>
    /// Draft ApprovalState from ApprovalRequired. Pending is reserved for future Submit.
    /// Public for unit-test access.
    /// </summary>
    public static string ResolveDraftApprovalState(bool approvalRequired) =>
        approvalRequired ? "NotSubmitted" : "NotRequired";

    private static TravelRequestDetailDto ToDetailDto(TravelRequest e, TravelRequestCycle? cycle = null)
    {
        return new TravelRequestDetailDto
        {
            CurrentCycle = ToCurrentTravelCycle(cycle),
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
            StartedAt = e.StartedAt,
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
            CurrentCycleNo = e.CurrentCycleNo,
            SubmittedAt = e.SubmittedAt,
            ApprovedAt = e.ApprovedAt,
            RejectedAt = e.RejectedAt,
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
