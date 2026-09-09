using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IWorkflowService
{
    Task<WorkflowResponseDto> StartAsync(StartWorkflowRequestDto dto, CancellationToken ct = default);
    Task<WorkflowResponseDto> GetOrCreateForBusinessRecordAsync(long businessModuleId, string businessRecordId, long? intakeRequestId, CancellationToken ct = default);
    Task<WorkflowResponseDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<WorkflowResponseDto?> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default);
    Task<WorkflowResponseDto> TransitionAsync(long id, TransitionWorkflowRequestDto dto, CancellationToken ct = default, bool preserveTatStartedAt = false);
    Task<List<WorkflowHistoryResponseDto>> GetHistoryAsync(long id, CancellationToken ct = default);
}
