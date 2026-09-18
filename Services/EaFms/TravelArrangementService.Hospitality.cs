using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public partial class TravelArrangementService
{
    public async Task<TravelHospitalityResponseDto> CreateHospitalityAsync(long travelRequestId, SaveTravelHospitalityDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var parent = await ParentAsync(travelRequestId, true, ct);
        EnsureReady(parent);
        var row = new TravelHospitality { TravelRequestId = parent.Id, CreatedBy = Actor, CreatedDate = Clock.UtcNowTz };
        Apply(row, dto);
        _db.Set<TravelHospitality>().Add(row);
        await _db.SaveChangesAsync(ct);
        var result = ToDto(row, parent.ReferenceNo);
        _audit.AddAudit("TRAVEL_HOSPITALITY_CREATE", "Travel", "TravelHospitality", row.Id.ToString(CultureInfo.InvariantCulture), null, result);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<List<TravelHospitalityResponseDto>> ListHospitalityAsync(long travelRequestId, CancellationToken ct = default)
    {
        var parent = await ParentAsync(travelRequestId, false, ct);
        var rows = await _db.Set<TravelHospitality>().AsNoTracking().Where(x => x.TravelRequestId == parent.Id && !x.IsDeleted)
            .OrderBy(x => x.CreatedDate).ThenBy(x => x.Id).ToListAsync(ct);
        return rows.Select(x => ToDto(x, parent.ReferenceNo)).ToList();
    }

    public async Task<TravelHospitalityResponseDto> GetHospitalityAsync(long id, CancellationToken ct = default)
    {
        var row = await _db.Set<TravelHospitality>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Travel Hospitality arrangement not found.");
        var parent = await ParentAsync(row.TravelRequestId, false, ct);
        return ToDto(row, parent.ReferenceNo);
    }

    public async Task<TravelHospitalityResponseDto> UpdateHospitalityAsync(long id, SaveTravelHospitalityDto dto, CancellationToken ct = default)
    {
        Validate(dto);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var parentId = await _db.Set<TravelHospitality>().AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => (long?)x.TravelRequestId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Travel Hospitality arrangement not found.");
        var parent = await ParentAsync(parentId, true, ct);
        EnsureReady(parent);
        var row = await _db.Set<TravelHospitality>().SingleOrDefaultAsync(x => x.Id == id && x.TravelRequestId == parent.Id && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Travel Hospitality arrangement not found.");
        if (_db.Database.IsRelational()) await _db.Entry(row).ReloadAsync(ct);
        if (row.IsDeleted) throw new NotFoundException("Travel Hospitality arrangement not found.");
        var previous = ToDto(row, parent.ReferenceNo);
        Apply(row, dto);
        row.ModifiedBy = Actor;
        row.ModifiedDate = Clock.UtcNowTz;
        var result = ToDto(row, parent.ReferenceNo);
        _audit.AddAudit("TRAVEL_HOSPITALITY_UPDATE", "Travel", "TravelHospitality", row.Id.ToString(CultureInfo.InvariantCulture), previous, result);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private static void Validate(SaveTravelHospitalityDto dto)
    {
        ValidateStatus(dto.Status);
        ValidateCost(dto.EstimatedCost);
        ValidateCost(dto.ActualCost);
        if (dto.Location?.Length > 500) throw new BadRequestException("Location must not exceed 500 characters.");
        if (dto.Provider?.Length > 200) throw new BadRequestException("Provider must not exceed 200 characters.");
        if (dto.Currency?.Length > 10) throw new BadRequestException("Currency must not exceed 10 characters.");
        if (dto.NumberOfGuests < 0) throw new BadRequestException("NumberOfGuests must be nonnegative.");
    }

    private static void Apply(TravelHospitality row, SaveTravelHospitalityDto dto)
    {
        row.Status = dto.Status ?? row.Status;
        row.ClientGuestDetails = dto.ClientGuestDetails;
        row.HospitalityRequirement = dto.HospitalityRequirement;
        row.MeetingEventPurpose = dto.MeetingEventPurpose;
        row.NumberOfGuests = dto.NumberOfGuests;
        row.SpecialArrangements = dto.SpecialArrangements;
        row.Location = dto.Location;
        row.ScheduledAt = dto.ScheduledAt;
        row.Provider = dto.Provider;
        row.EstimatedCost = dto.EstimatedCost;
        row.ActualCost = dto.ActualCost;
        row.Currency = dto.Currency;
        row.Notes = dto.Notes;
    }

    private static TravelHospitalityResponseDto ToDto(TravelHospitality row, string referenceNo) => new()
    {
        HospitalityId = row.Id, TravelRequestId = row.TravelRequestId, TravelReferenceNo = referenceNo,
        Status = row.Status,
        ClientGuestDetails = row.ClientGuestDetails,
        HospitalityRequirement = row.HospitalityRequirement,
        MeetingEventPurpose = row.MeetingEventPurpose,
        NumberOfGuests = row.NumberOfGuests,
        SpecialArrangements = row.SpecialArrangements,
        Location = row.Location,
        ScheduledAt = row.ScheduledAt,
        Provider = row.Provider,
        EstimatedCost = row.EstimatedCost,
        ActualCost = row.ActualCost,
        Currency = row.Currency,
        Notes = row.Notes,
        CreatedBy = row.CreatedBy, CreatedAt = row.CreatedDate, UpdatedAt = row.ModifiedDate
    };
}
