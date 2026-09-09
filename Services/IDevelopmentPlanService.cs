using Jarvis5.Dtos.DevelopmentPlan;

namespace Jarvis5.Services;

public interface IDevelopmentPlanService
{
    Task<List<TaskModuleDetailDto>> CreateAsync(long requestId, CreateDevelopmentPlanDto dto, CancellationToken ct = default);
    Task<DevelopmentPlanDto> GetByRequestIdAsync(long requestId, CancellationToken ct = default);
    Task<TaskModuleDetailDto> GetModuleByIdAsync(long taskId, CancellationToken ct = default);
    Task<List<TaskHistoryDto>> GetHistoryByRequestIdAsync(long requestId, CancellationToken ct = default);
    Task<TaskModuleDetailDto> UpdateModuleAsync(long taskId, UpdateTaskModuleDto dto, CancellationToken ct = default);
    Task<TaskModuleDetailDto> UpdateStageAsync(long taskId, long stageId, UpdateStageDto dto, CancellationToken ct = default);
    Task DeleteModuleAsync(long taskId, CancellationToken ct = default);
}
