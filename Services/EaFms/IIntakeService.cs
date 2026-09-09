using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IIntakeService
{
    Task<IntakeRequestResponseDto> CreateAsync(CreateIntakeRequestDto dto, CancellationToken ct = default);
    Task<IntakeRequestResponseDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<(List<IntakeRequestResponseDto> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search, CancellationToken ct = default);
    Task<IntakeRequestResponseDto> UpdateAsync(long id, UpdateIntakeRequestDto dto, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);

    Task<IntakeClassificationResponseDto> CreateClassificationAsync(long intakeId, CreateIntakeClassificationDto dto, CancellationToken ct = default);
    Task<List<IntakeClassificationResponseDto>> GetClassificationsAsync(long intakeId, CancellationToken ct = default);
    Task<IntakeClassificationResponseDto> UpdateClassificationAsync(long intakeId, long classificationId, UpdateIntakeClassificationDto dto, CancellationToken ct = default);
    Task DeleteClassificationAsync(long intakeId, long classificationId, CancellationToken ct = default);
}
