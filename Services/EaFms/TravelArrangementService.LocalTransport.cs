using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public partial class TravelArrangementService
{
    public async Task<TravelLocalTransportResponseDto> CreateLocalTransportAsync(long travelRequestId, SaveTravelLocalTransportDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var parent = await ParentAsync(travelRequestId, true, ct);
        EnsureReady(parent);
        var row = new TravelLocalTransport { TravelRequestId = parent.Id, CreatedBy = Actor, CreatedDate = Clock.UtcNowTz };
        Apply(row, dto);
        _db.Set<TravelLocalTransport>().Add(row);
        await _db.SaveChangesAsync(ct);
        var result = ToDto(row, parent.ReferenceNo);
        _audit.AddAudit("TRAVEL_LOCAL_TRANSPORT_CREATE", "Travel", "TravelLocalTransport", row.Id.ToString(CultureInfo.InvariantCulture), null, result);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<List<TravelLocalTransportResponseDto>> ListLocalTransportAsync(long travelRequestId, CancellationToken ct = default)
    {
        var parent = await ParentAsync(travelRequestId, false, ct);
        var rows = await _db.Set<TravelLocalTransport>().AsNoTracking().Where(x => x.TravelRequestId == parent.Id && !x.IsDeleted)
            .OrderBy(x => x.CreatedDate).ThenBy(x => x.Id).ToListAsync(ct);
        return rows.Select(x => ToDto(x, parent.ReferenceNo)).ToList();
    }

    public async Task<TravelLocalTransportResponseDto> GetLocalTransportAsync(long id, CancellationToken ct = default)
    {
        var row = await _db.Set<TravelLocalTransport>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Travel LocalTransport arrangement not found.");
        var parent = await ParentAsync(row.TravelRequestId, false, ct);
        return ToDto(row, parent.ReferenceNo);
    }

    public async Task<TravelLocalTransportResponseDto> UpdateLocalTransportAsync(long id, SaveTravelLocalTransportDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var parentId = await _db.Set<TravelLocalTransport>().AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => (long?)x.TravelRequestId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Travel LocalTransport arrangement not found.");
        var parent = await ParentAsync(parentId, true, ct);
        EnsureReady(parent);
        var row = await _db.Set<TravelLocalTransport>().SingleOrDefaultAsync(x => x.Id == id && x.TravelRequestId == parent.Id && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Travel LocalTransport arrangement not found.");
        if (_db.Database.IsRelational()) await _db.Entry(row).ReloadAsync(ct);
        if (row.IsDeleted) throw new NotFoundException("Travel LocalTransport arrangement not found.");
        var previous = ToDto(row, parent.ReferenceNo);
        Apply(row, dto);
        row.ModifiedBy = Actor;
        row.ModifiedDate = Clock.UtcNowTz;
        var result = ToDto(row, parent.ReferenceNo);
        _audit.AddAudit("TRAVEL_LOCAL_TRANSPORT_UPDATE", "Travel", "TravelLocalTransport", row.Id.ToString(CultureInfo.InvariantCulture), previous, result);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static void Validate(SaveTravelLocalTransportDto dto)
    {
        ValidateStatus(dto.TransportStatus);
        ValidateCost(dto.EstimatedCost);
        ValidateCost(dto.ActualCost);
        if (dto.TransportType?.Length > 50) throw new BadRequestException("TransportType must not exceed 50 characters.");
        if (dto.PickupLocation?.Length > 500) throw new BadRequestException("PickupLocation must not exceed 500 characters.");
        if (dto.DropLocation?.Length > 500) throw new BadRequestException("DropLocation must not exceed 500 characters.");
        if (dto.VehiclePreference?.Length > 200) throw new BadRequestException("VehiclePreference must not exceed 200 characters.");
        if (dto.BookingReference?.Length > 200) throw new BadRequestException("BookingReference must not exceed 200 characters.");
        if (dto.Provider?.Length > 200) throw new BadRequestException("Provider must not exceed 200 characters.");
        if (dto.Currency?.Length > 10) throw new BadRequestException("Currency must not exceed 10 characters.");
        if (dto.TransportType is not (null or "AirportPickup" or "StationPickup" or "LocalTransportation" or "Other"))
            throw new BadRequestException("Unsupported transportType.");
    }

    private static void Apply(TravelLocalTransport row, SaveTravelLocalTransportDto dto)
    {
        row.TransportStatus = dto.TransportStatus ?? row.TransportStatus;
        row.TransportType = dto.TransportType;
        row.PickupLocation = dto.PickupLocation;
        row.DropLocation = dto.DropLocation;
        row.VehiclePreference = dto.VehiclePreference;
        row.BookingReference = dto.BookingReference;
        row.ScheduledAt = dto.ScheduledAt;
        row.Provider = dto.Provider;
        row.EstimatedCost = dto.EstimatedCost;
        row.ActualCost = dto.ActualCost;
        row.Currency = dto.Currency;
        row.Notes = dto.Notes;
    }

    private static TravelLocalTransportResponseDto ToDto(TravelLocalTransport row, string referenceNo) => new()
    {
        LocalTransportId = row.Id, TravelRequestId = row.TravelRequestId, TravelReferenceNo = referenceNo,
        TransportStatus = row.TransportStatus,
        TransportType = row.TransportType,
        PickupLocation = row.PickupLocation,
        DropLocation = row.DropLocation,
        VehiclePreference = row.VehiclePreference,
        BookingReference = row.BookingReference,
        ScheduledAt = row.ScheduledAt,
        Provider = row.Provider,
        EstimatedCost = row.EstimatedCost,
        ActualCost = row.ActualCost,
        Currency = row.Currency,
        Notes = row.Notes,
        CreatedBy = row.CreatedBy, CreatedAt = row.CreatedDate, UpdatedAt = row.ModifiedDate
    };
}
