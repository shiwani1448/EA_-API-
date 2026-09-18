using Jarvis5.Dtos.EaFms;
namespace Jarvis5.Services.EaFms;

public interface ITravelBookingService
{
    Task<TravelBookingResponseDto> CreateAsync(long travelRequestId, CreateTravelBookingDto dto, CancellationToken ct = default);
    Task<List<TravelBookingResponseDto>> ListAsync(long travelRequestId, CancellationToken ct = default);
    Task<TravelBookingResponseDto> UpdateAsync(long bookingId, UpdateTravelBookingDto dto, CancellationToken ct = default);
    Task<TravelBookingResponseDto> CancelAsync(long bookingId, CancellationToken ct = default);
}
