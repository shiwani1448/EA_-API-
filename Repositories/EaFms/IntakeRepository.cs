using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class IntakeRepository : IIntakeRepository
{
    private readonly EaFmsDbContext _context;

    public IntakeRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(IntakeRequest request, CancellationToken ct = default) =>
        await _context.IntakeRequests.AddAsync(request, ct);

    public Task<IntakeRequest?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _context.IntakeRequests
            .Include(r => r.Classifications)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, ct);

    public void Update(IntakeRequest request) => _context.IntakeRequests.Update(request);

    public async Task<(List<IntakeRequest> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = _context.IntakeRequests.AsNoTracking().Where(r => !r.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => EF.Functions.ILike(r.Title, $"%{term}%") || EF.Functions.ILike(r.Description ?? string.Empty, $"%{term}%"));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddClassificationAsync(IntakeClassification classification, CancellationToken ct = default) =>
        await _context.IntakeClassifications.AddAsync(classification, ct);

    public Task<List<IntakeClassification>> GetClassificationsAsync(long intakeRequestId, CancellationToken ct = default) =>
        _context.IntakeClassifications
            .AsNoTracking()
            .Where(c => c.IntakeRequestId == intakeRequestId && !c.IsDeleted)
            .ToListAsync(ct);

    public Task<IntakeClassification?> GetClassificationByIdAsync(long id, CancellationToken ct = default) =>
        _context.IntakeClassifications.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

    public void UpdateClassification(IntakeClassification classification) => _context.IntakeClassifications.Update(classification);

    public void DeleteClassification(IntakeClassification classification)
    {
        classification.IsDeleted = true;
        _context.IntakeClassifications.Update(classification);
    }
}
