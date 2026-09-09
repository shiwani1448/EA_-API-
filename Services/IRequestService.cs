using Jarvis5.Common;
using Jarvis5.Dtos;

namespace Jarvis5.Services;

public interface IRequestService
{
    Task<RequestDetailDto> CreateAsync(CreateRequestDto dto, CancellationToken ct = default);
    Task<RequestDetailDto> UpdateAsync(long id, UpdateRequestDto dto, CancellationToken ct = default);
    Task<RequestDetailDto> UpdateOverallDatesAsync(long id, UpdateRequestOverallDatesDto dto, CancellationToken ct = default);
    Task<PagedResult<RequestListItemDto>> GetPagedAsync(RequestFilterDto filter, CancellationToken ct = default);
    Task<RequestDetailDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<List<RequestHistoryDto>> GetHistoryAsync(long id, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
