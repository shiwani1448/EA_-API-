using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly EaFmsDbContext _context;

    public WorkflowRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(WorkflowInstance instance, CancellationToken ct = default) =>
        await _context.WorkflowInstances.AddAsync(instance, ct);

    public Task<WorkflowInstance?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _context.WorkflowInstances
            .Include(w => w.History)
            .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted, ct);

    public Task<WorkflowInstance?> GetByIntakeRequestIdAsync(long intakeRequestId, CancellationToken ct = default) =>
        _context.WorkflowInstances
            .Include(w => w.History)
            .FirstOrDefaultAsync(w => w.IntakeRequestId == intakeRequestId && w.IsActive && !w.IsDeleted, ct);

    public void Update(WorkflowInstance instance) => _context.WorkflowInstances.Update(instance);

    public async Task AddHistoryAsync(WorkflowHistory history, CancellationToken ct = default) =>
        await _context.WorkflowHistory.AddAsync(history, ct);

    public Task<List<WorkflowHistory>> GetHistoryAsync(long workflowInstanceId, CancellationToken ct = default) =>
        _context.WorkflowHistory
            .AsNoTracking()
            .Where(h => h.WorkflowInstanceId == workflowInstanceId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(ct);
}
