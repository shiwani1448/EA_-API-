using Jarvis5.Entities.EaFms;

namespace Jarvis5.Repositories.EaFms;

public interface IIntakeRepository
{
    Task AddAsync(IntakeRequest request, CancellationToken ct = default);
    Task<IntakeRequest?> GetByIdAsync(long id, CancellationToken ct = default);
    void Update(IntakeRequest request);
    Task<(List<IntakeRequest> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search, CancellationToken ct = default);

    Task AddClassificationAsync(IntakeClassification classification, CancellationToken ct = default);
    Task<List<IntakeClassification>> GetClassificationsAsync(long intakeRequestId, CancellationToken ct = default);
    Task<IntakeClassification?> GetClassificationByIdAsync(long id, CancellationToken ct = default);
    void UpdateClassification(IntakeClassification classification);
    void DeleteClassification(IntakeClassification classification);
}
