using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IEmReportService
{
    Task<EmReportOverviewResponseDto> GetOverviewAsync(EmReportOverviewQueryDto query, CancellationToken ct);
    Task<EmReportAttentionResponseDto> GetAttentionAsync(EmReportOverviewQueryDto query, CancellationToken ct);
    Task<IReadOnlyList<EmReportModuleSummaryDto>> GetModulesAsync(EmReportOverviewQueryDto query, CancellationToken ct);
    Task<Jarvis5.Common.PagedResult<EmReportTaskRowDto>> GetTasksAsync(EmReportTaskRegisterQueryDto query, CancellationToken ct);
}
