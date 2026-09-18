using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface ITravelArrangementService
{
    Task<TravelLocalTransportResponseDto> CreateLocalTransportAsync(long travelRequestId, SaveTravelLocalTransportDto dto, CancellationToken ct = default);
    Task<List<TravelLocalTransportResponseDto>> ListLocalTransportAsync(long travelRequestId, CancellationToken ct = default);
    Task<TravelLocalTransportResponseDto> GetLocalTransportAsync(long id, CancellationToken ct = default);
    Task<TravelLocalTransportResponseDto> UpdateLocalTransportAsync(long id, SaveTravelLocalTransportDto dto, CancellationToken ct = default);
    Task<TravelHospitalityResponseDto> CreateHospitalityAsync(long travelRequestId, SaveTravelHospitalityDto dto, CancellationToken ct = default);
    Task<List<TravelHospitalityResponseDto>> ListHospitalityAsync(long travelRequestId, CancellationToken ct = default);
    Task<TravelHospitalityResponseDto> GetHospitalityAsync(long id, CancellationToken ct = default);
    Task<TravelHospitalityResponseDto> UpdateHospitalityAsync(long id, SaveTravelHospitalityDto dto, CancellationToken ct = default);
}
