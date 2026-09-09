using Jarvis5.Data;
using Jarvis5.Dtos;
using Jarvis5.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories;

public class RequestRepository : IRequestRepository
{
    private readonly AppDbContext _context;

    public RequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextRequestNoAsync(CancellationToken ct = default)
    {
        var next = await _context.Database
            .SqlQueryRaw<long>($"SELECT nextval('{AppDbContext.RequestNoSequence}') AS \"Value\"")
            .SingleAsync(ct);

        return $"REQ-{next:D6}";
    }

    public Task<SCIHRequest?> GetByIdAsync(long id, CancellationToken ct = default) =>
        _context.Requests.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExistsIgnoringSoftDeleteAsync(long id, CancellationToken ct = default) =>
        _context.Requests.IgnoreQueryFilters().AnyAsync(r => r.Id == id, ct);

    public async Task AddAsync(SCIHRequest request, CancellationToken ct = default) =>
        await _context.Requests.AddAsync(request, ct);

    public void Update(SCIHRequest request) => _context.Requests.Update(request);

    public async Task<(List<SCIHRequest> Items, int TotalCount)> GetPagedAsync(RequestFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.Requests.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Department))
            query = query.Where(r => r.DepartmentId == filter.Department);

        if (!string.IsNullOrWhiteSpace(filter.RaisedBy))
            query = query.Where(r => r.RaisedBy == filter.RaisedBy);

        if (!string.IsNullOrWhiteSpace(filter.Priority))
            query = query.Where(r => r.Priority == filter.Priority);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(r => r.Status == filter.Status);

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.RaisedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(r => r.RaisedAt <= filter.ToDate.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(r => EF.Functions.ILike(r.Title, term) || EF.Functions.ILike(r.RequestNo, term));
        }

        var totalCount = await query.CountAsync(ct);

        var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 20 : filter.PageSize;

        var items = await query
            .OrderByDescending(r => r.RaisedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<List<SCIHRequest>> GetSimilarityCandidatesAsync(long excludeRequestId, int maxCandidates, CancellationToken ct = default) =>
        _context.Requests
            .AsNoTracking()
            .Where(r => r.Id != excludeRequestId)
            .OrderByDescending(r => r.RaisedAt)
            .Take(maxCandidates)
            .ToListAsync(ct);
}
