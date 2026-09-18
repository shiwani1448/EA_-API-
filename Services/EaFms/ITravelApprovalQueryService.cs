using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface ITravelApprovalQueryService
{
    Task<PagedResult<TravelPendingApprovalDto>> PendingAsync(TravelPendingApprovalQueryDto query, CancellationToken ct = default);
    Task<TravelApprovalDetailDto> GetAsync(long travelRequestId, CancellationToken ct = default);
}
