using Jarvis5.Dtos.EaFms;

namespace Jarvis5.Services.EaFms;

public interface IWorkflowExecutionService
{
    Task<WorkflowResponseDto> StartAsync(long workflowId, StartWorkRequestDto dto, CancellationToken ct);
    Task<SimplePauseResponseDto> PauseAsync(long workflowId, SimplePauseRequestDto dto, CancellationToken ct);
    Task<SimplePauseResponseDto> ResumePauseAsync(long workflowId, SimpleResumeRequestDto dto, CancellationToken ct);
    Task<WorkflowWaitingResponseDto> WaitAsync(long workflowId, CreateWorkPauseRequestDto dto, CancellationToken ct);
    Task<WorkflowWaitingResponseDto> ContinueAsync(long workflowId, long waitingId, ResumeWorkPauseRequestDto dto, CancellationToken ct);
    Task<CompleteWorkResponseDto> CompleteAsync(long workflowId, CompleteWorkRequestDto dto, CancellationToken ct);
    Task<WorkflowWaitingListResponseDto> GetWaitingAsync(long workflowId, CancellationToken ct);
    Task<WorkflowWaitingResponseDto> GetWaitingAsync(long workflowId, long waitingId, CancellationToken ct);
    Task<PauseSummaryResponseDto> GetSummaryAsync(long workflowId, CancellationToken ct);
}
