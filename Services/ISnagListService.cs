using Jarvis5.Dtos.SnagList;

namespace Jarvis5.Services;

public interface ISnagListService
{
    Task<SnagListDetailDto> CreateAsync(CreateSnagListDto dto, CancellationToken ct = default);
    Task<SnagListDto> GetByIdAsync(long snagId, CancellationToken ct = default);

    /// <summary>Every Snag List that has this employee assigned as the doer on at
    /// least one selected stage.</summary>
    Task<List<SnagListDetailDto>> GetByDoerIdAsync(string employeeId, CancellationToken ct = default);
    Task<SnagListDetailDto> UpdateAsync(long snagId, UpdateSnagListDto dto, CancellationToken ct = default);
    Task<SnagListDetailDto> UpdateStageAsync(long snagId, long stageId, UpdateSnagStageDto dto, CancellationToken ct = default);
    Task<SnagListDetailDto> CloseAsync(long snagId, CancellationToken ct = default);
}
