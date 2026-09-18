using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Common.EaFms;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class TravelBookingService(EaFmsDbContext db, ICurrentUserService user, IAuditService audit) : ITravelBookingService
{
    private string Actor => user.UserName ?? user.UserId.ToString(CultureInfo.InvariantCulture);

    public async Task<TravelBookingResponseDto> CreateAsync(long travelRequestId, CreateTravelBookingDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        if (!TravelBookingRules.IsType(dto.BookingType)) throw new BadRequestException("Unsupported bookingType.");
        var status = dto.BookingStatus ?? TravelBookingRules.Requested;
        if (status is not (TravelBookingRules.Requested or TravelBookingRules.NotRequired))
            throw new BusinessRuleException("New bookings must start as Requested or NotRequired.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var parent = await ParentAsync(travelRequestId, true, ct);
        EnsureReady(parent);
        var booking = new TravelBooking { TravelRequestId = parent.Id, BookingType = dto.BookingType,
            BookingStatus = status, CreatedBy = Actor, CreatedDate = Clock.UtcNowTz };
        Apply(booking, dto);
        db.TravelBookings.Add(booking);
        await db.SaveChangesAsync(ct); // obtain BookingId before adding its audit, in the same transaction
        AddAudit("TRAVEL_BOOKING_CREATE", parent, booking, null);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(booking, parent.ReferenceNo);
    }

    public async Task<List<TravelBookingResponseDto>> ListAsync(long travelRequestId, CancellationToken ct = default)
    {
        var parent = await ParentAsync(travelRequestId, false, ct);
        var rows = await db.TravelBookings.AsNoTracking().Where(b => b.TravelRequestId == parent.Id && !b.IsDeleted)
            .OrderBy(b => b.CreatedDate).ThenBy(b => b.Id).ToListAsync(ct);
        return rows.Select(b => ToDto(b, parent.ReferenceNo)).ToList();
    }

    public async Task<TravelBookingResponseDto> UpdateAsync(long bookingId, UpdateTravelBookingDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (booking, parent) = await BookingAsync(bookingId, ct);
        EnsureReady(parent);
        var next = dto.BookingStatus ?? booking.BookingStatus;
        if (!TravelBookingRules.CanTransition(booking.BookingStatus, next))
            throw new BusinessRuleException("Booking status transition is not allowed.");
        var previous = ToDto(booking, parent.ReferenceNo);
        booking.BookingStatus = next;
        Apply(booking, dto);
        Touch(booking);
        AddAudit(next == TravelBookingRules.Cancelled && previous.BookingStatus != next
            ? "TRAVEL_BOOKING_CANCEL" : "TRAVEL_BOOKING_UPDATE", parent, booking, previous);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(booking, parent.ReferenceNo);
    }

    public async Task<TravelBookingResponseDto> CancelAsync(long bookingId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (booking, parent) = await BookingAsync(bookingId, ct);
        if (booking.BookingStatus is not (TravelBookingRules.Requested or TravelBookingRules.InProgress or TravelBookingRules.Booked))
            throw new BusinessRuleException("Booking cannot be cancelled in its current state.");
        var previous = ToDto(booking, parent.ReferenceNo);
        booking.BookingStatus = TravelBookingRules.Cancelled;
        Touch(booking);
        AddAudit("TRAVEL_BOOKING_CANCEL", parent, booking, previous);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(booking, parent.ReferenceNo);
    }

    private static void EnsureReady(TravelRequest parent)
    {
        if (parent.BusinessState != "Upcoming" || (parent.ApprovalRequired
                ? parent.ApprovalState != "Approved" : parent.ApprovalState != "NotRequired"))
            throw new BusinessRuleException("Travel request is not ready for booking execution.");
    }

    // Same parent-first FOR UPDATE convention as Travel lifecycle writers. Serializes
    // transitions and duplicate cancellation without adding a version/approval system.
    private async Task<TravelRequest> ParentAsync(long id, bool locked, CancellationToken ct)
    {
        TravelRequest? parent;
        if (locked && db.Database.IsRelational())
        {
            var rows = await db.TravelRequests.FromSqlInterpolated(
                $"SELECT * FROM public.ea_travel_requests WHERE \"Id\" = {id} AND NOT \"IsDeleted\" FOR UPDATE").ToListAsync(ct);
            parent = rows.SingleOrDefault();
            if (parent != null) await db.Entry(parent).ReloadAsync(ct);
        }
        else parent = await db.TravelRequests.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        return parent ?? throw new NotFoundException($"Travel request {id} not found.");
    }

    private async Task<(TravelBooking Booking, TravelRequest Parent)> BookingAsync(long id, CancellationToken ct)
    {
        var parentId = await db.TravelBookings.AsNoTracking().Where(b => b.Id == id && !b.IsDeleted)
            .Select(b => (long?)b.TravelRequestId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Travel booking not found.");
        var parent = await ParentAsync(parentId, true, ct);
        var booking = await db.TravelBookings.SingleOrDefaultAsync(b => b.Id == id && b.TravelRequestId == parent.Id && !b.IsDeleted, ct)
            ?? throw new NotFoundException("Travel booking not found.");
        if (db.Database.IsRelational()) await db.Entry(booking).ReloadAsync(ct);
        if (booking.IsDeleted) throw new NotFoundException("Travel booking not found.");
        return (booking, parent);
    }

    private static void Validate(UpdateTravelBookingDto dto)
    {
        if (dto.BookingStatus != null && !TravelBookingRules.IsStatus(dto.BookingStatus))
            throw new BadRequestException("Unsupported bookingStatus.");
        if (dto.Cost is < 0 or > 9999999999999999.99m || (dto.Cost.HasValue && decimal.Round(dto.Cost.Value, 2) != dto.Cost))
            throw new BadRequestException("Cost must be nonnegative, fit numeric(18,2), and have at most two decimal places.");
        if (dto.Provider?.Length > 200 || dto.BookingReference?.Length > 200 || dto.Currency?.Length > 10)
            throw new BadRequestException("Provider/reference must not exceed 200 characters; currency must not exceed 10.");
    }

    private void Touch(TravelBooking booking) { booking.ModifiedBy = Actor; booking.ModifiedDate = Clock.UtcNowTz; }
    private void AddAudit(string action, TravelRequest parent, TravelBooking booking, TravelBookingResponseDto? previous) =>
        audit.AddAudit(action, "Travel", "TravelBooking", booking.Id.ToString(CultureInfo.InvariantCulture),
            previous, ToDto(booking, parent.ReferenceNo), "Travel booking execution changed");

    private static void Apply(TravelBooking booking, UpdateTravelBookingDto dto)
    {
        booking.Provider = dto.Provider;
        booking.BookingReference = dto.BookingReference;
        booking.BookingDate = dto.BookingDate;
        booking.DepartureDetails = dto.DepartureDetails;
        booking.ArrivalDetails = dto.ArrivalDetails;
        booking.HotelDetails = dto.HotelDetails;
        booking.VehicleDetails = dto.VehicleDetails;
        booking.Cost = dto.Cost;
        booking.Currency = dto.Currency;
        booking.Notes = dto.Notes;
    }
    private static TravelBookingResponseDto ToDto(TravelBooking b, string referenceNo) => new()
    {
        BookingId = b.Id, TravelRequestId = b.TravelRequestId, TravelReferenceNo = referenceNo,
        BookingType = b.BookingType, BookingStatus = b.BookingStatus,
        Provider = b.Provider,
        BookingReference = b.BookingReference,
        BookingDate = b.BookingDate,
        DepartureDetails = b.DepartureDetails,
        ArrivalDetails = b.ArrivalDetails,
        HotelDetails = b.HotelDetails,
        VehicleDetails = b.VehicleDetails,
        Cost = b.Cost,
        Currency = b.Currency,
        Notes = b.Notes,
        CreatedBy = b.CreatedBy, CreatedAt = b.CreatedDate, UpdatedAt = b.ModifiedDate
    };
}
