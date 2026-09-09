using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface IWorkflowRepository
{
    Task AddAsync(WorkflowInstance instance, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default);
    void Update(WorkflowInstance instance);

    Task AddHistoryAsync(WorkflowHistory history, CancellationToken ct = default);
    Task<List<WorkflowHistory>> GetHistoryAsync(long workflowInstanceId, CancellationToken ct = default);
}
