using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IFollowupService
{
    Task<FollowupResponseDto> CreateAsync(CreateFollowupRequestDto dto, CancellationToken ct = default);
    Task<FollowupResponseDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<List<FollowupResponseDto>> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default);
    Task<FollowupResponseDto> UpdateAsync(long id, UpdateFollowupRequestDto dto, CancellationToken ct = default);
    Task<FollowupResponseDto> CompleteAsync(long id, CompleteFollowupRequestDto dto, CancellationToken ct = default);
    Task<PagedResult<FollowupResponseDto>> GetPagedAsync(FollowupListQueryDto query, CancellationToken ct = default);
    Task<FollowupSummaryResponseDto> GetSummaryAsync(FollowupListQueryDto query, CancellationToken ct = default);
    Task<List<FollowupResponseDto>> GetBySourceAsync(long moduleId, string recordId, CancellationToken ct = default);
    Task RecordFollowupAsync(long id, RecordFollowupRequestDto dto, CancellationToken ct = default);
    Task SendEmailAsync(long id, CancellationToken ct = default);
    Task<FollowupWhatsAppActionResponseDto> SendWhatsAppAsync(long id, CancellationToken ct = default);
}
