using Jarvis5.Common;
using Jarvis5.Data;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class AnalysisRepository : IAnalysisRepository
{
    private readonly AppDbContext _context;

    public AnalysisRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<SCIHAnalysis?> GetByRequestIdAsync(long requestId, CancellationToken ct = default) =>
        _context.Analyses
            .Where(a => a.RequestId == requestId)
            .OrderByDescending(a => a.Version)
            .FirstOrDefaultAsync(ct);

    public Task<SCIHAnalysis?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _context.Analyses.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<SCIHAnalysis>> GetVersionsByRequestIdAsync(long requestId, CancellationToken ct = default) =>
        _context.Analyses
            .AsNoTracking()
            .Where(a => a.RequestId == requestId)
            .OrderBy(a => a.Version)
            .ToListAsync(ct);

    public async Task<Dictionary<long, string>> GetExecutiveSummariesAsync(IReadOnlyCollection<long> requestIds, CancellationToken ct = default)
    {
        if (requestIds.Count == 0) return new Dictionary<long, string>();

        var rows = await _context.Analyses
            .AsNoTracking()
            .Where(a => requestIds.Contains(a.RequestId))
            .Select(a => new { a.RequestId, a.Version, a.AnalysisJson })
            .ToListAsync(ct);

        var result = new Dictionary<long, string>();
        foreach (var group in rows.GroupBy(r => r.RequestId))
        {
            var latest = group.OrderByDescending(r => r.Version).First();
            var summary = JsonHelper.DeserializeObjectOrDefault<AiAnalysisResultDto>(latest.AnalysisJson).ExecutiveSummary;
            if (!string.IsNullOrWhiteSpace(summary))
                result[group.Key] = summary;
        }

        return result;
    }

    public async Task AddAsync(SCIHAnalysis analysis, CancellationToken ct = default) =>
        await _context.Analyses.AddAsync(analysis, ct);

    public void Update(SCIHAnalysis analysis) => _context.Analyses.Update(analysis);
}
