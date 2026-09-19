using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IEscalationService
{
    Task<EscalationResponseDto> CreateAsync(CreateEscalationRequestDto dto, CancellationToken ct = default);
    Task<EscalationResponseDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<List<EscalationResponseDto>> GetByFollowupIdAsync(long followupId, CancellationToken ct = default);
    Task<PagedResult<EscalationResponseDto>> GetPagedAsync(EscalationListQueryDto query, CancellationToken ct = default);
    Task ResolveAsync(long id, ResolveEscalationRequestDto dto, CancellationToken ct = default);
    Task AcknowledgeAsync(long id, string? acknowledgementNote, CancellationToken ct = default);
    Task<List<EscalationLevelResponseDto>> GetActiveLevelsAsync(CancellationToken ct = default);
}
