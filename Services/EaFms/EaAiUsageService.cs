using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public interface IEaAiUsageService
{
    Task<PagedResult<EaAiUsageRowDto>> ListAsync(EaAiUsageQueryDto query, CancellationToken ct);
    Task<EaAiUsageDetailDto> GetAsync(long id, CancellationToken ct);
    Task<EaAiUsageSummaryDto> SummaryAsync(EaAiUsageQueryDto query, CancellationToken ct);
    Task<EaAiTaskUsageDetailDto> ForTaskAsync(long eaTaskId, CancellationToken ct);
}

/// <summary>Read-only views over ea_ai_usage_logs: who used AI, where, for what, and how many tokens.</summary>
public sealed class EaAiUsageService(EaFmsDbContext db) : IEaAiUsageService
{
    private static readonly string[] Statuses = ["Succeeded", "InvalidJson", "Failed"];

    public async Task<PagedResult<EaAiUsageRowDto>> ListAsync(EaAiUsageQueryDto query, CancellationToken ct)
    {
        if (query.Page < 1) throw new BadRequestException("Page must be 1 or greater.");
        if (query.PageSize is < 1 or > 200) throw new BadRequestException("PageSize must be between 1 and 200.");
        var rows = await FilteredAsync(query, ct);
        var ordered = rows.OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.Id).ToList();
        return new PagedResult<EaAiUsageRowDto>
        {
            PageNumber = query.Page, PageSize = query.PageSize, TotalCount = ordered.Count,
            Items = ordered.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(r => Row(new EaAiUsageRowDto(), r)).ToList(),
        };
    }

    public async Task<EaAiUsageDetailDto> GetAsync(long id, CancellationToken ct)
    {
        var r = await db.AiUsageLogs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"AI usage log {id} not found.");
        var dto = Row(new EaAiUsageDetailDto(), r);
        dto.CompletedAt = r.CompletedAt; dto.ErrorMessage = r.ErrorMessage; dto.MessageId = r.MessageId; dto.StopReason = r.StopReason;
        dto.SystemPrompt = r.SystemPrompt; dto.UserPrompt = r.UserPrompt; dto.ResponseText = r.ResponseText;
        dto.CacheCreationInputTokens = r.CacheCreationInputTokens; dto.CacheReadInputTokens = r.CacheReadInputTokens;
        dto.UsedById = r.UsedById; dto.UsedValue = r.UsedValue;
        return dto;
    }

    public async Task<EaAiUsageSummaryDto> SummaryAsync(EaAiUsageQueryDto query, CancellationToken ct)
    {
        var rows = await FilteredAsync(query, ct);
        return new EaAiUsageSummaryDto
        {
            Total = Totals("Total", rows),
            ByModule = Group(rows, r => r.Module),
            ByFeature = Group(rows, r => $"{r.Module}: {r.Feature}"),
            ByEmployee = Group(rows, r => r.RequestedByName ?? r.RequestedById ?? "(Unknown)"),
            ByTask = rows.Where(r => r.BusinessRecordId is not null || r.EaTaskId is not null)
                .GroupBy(r => (r.Module, r.BusinessRecordId))
                .Select(g =>
                {
                    var t = Totals($"{g.Key.Module}:{g.Key.BusinessRecordId}", g);
                    return new EaAiTaskUsageDto
                    {
                        Key = t.Key, Calls = t.Calls, Succeeded = t.Succeeded, InvalidJson = t.InvalidJson, Failed = t.Failed, Used = t.Used, NotUsed = t.NotUsed,
                        InputTokens = t.InputTokens, OutputTokens = t.OutputTokens, TotalTokens = t.TotalTokens, LastUsedAt = t.LastUsedAt,
                        Module = g.Key.Module, BusinessRecordId = g.Key.BusinessRecordId, EaTaskId = g.Select(r => r.EaTaskId).FirstOrDefault(x => x.HasValue),
                    };
                })
                .OrderByDescending(t => t.TotalTokens).ThenByDescending(t => t.Calls).Take(100).ToList(),
            ByDay = Group(rows, r => IndiaBusinessCalendar.ToIndiaDate(r.RequestedAt).ToString("yyyy-MM-dd"))
                .OrderBy(d => d.Key, StringComparer.Ordinal).ToList(),
        };
    }

    public async Task<EaAiTaskUsageDetailDto> ForTaskAsync(long eaTaskId, CancellationToken ct)
    {
        var rows = await db.AiUsageLogs.AsNoTracking().Where(r => r.EaTaskId == eaTaskId).ToListAsync(ct);
        return new EaAiTaskUsageDetailDto
        {
            EaTaskId = eaTaskId,
            Total = Totals($"EaTask:{eaTaskId}", rows),
            ByFeature = Group(rows, r => $"{r.Module}: {r.Feature}"),
            Calls = rows.OrderBy(r => r.RequestedAt).ThenBy(r => r.Id).Select(r => Row(new EaAiUsageRowDto(), r)).ToList(),
        };
    }

    private async Task<List<EaAiUsageLog>> FilteredAsync(EaAiUsageQueryDto query, CancellationToken ct)
    {
        if (query.From > query.To) throw new BadRequestException("From must be on or before To.");
        var module = Allowed(query.Module, EaAiModules.All, "Module");
        var status = Allowed(query.Status, Statuses, "Status");

        var q = db.AiUsageLogs.AsNoTracking().AsQueryable();
        if (module is not null) q = q.Where(r => r.Module == module);
        if (status is not null) q = q.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(query.BusinessRecordId)) { var rid = query.BusinessRecordId.Trim(); q = q.Where(r => r.BusinessRecordId == rid); }
        if (query.EaTaskId.HasValue) q = q.Where(r => r.EaTaskId == query.EaTaskId);
        if (query.Used.HasValue) q = q.Where(r => r.IsUsed == query.Used.Value);
        if (query.From.HasValue) q = q.Where(r => r.RequestedAt >= query.From.Value);
        if (query.To.HasValue) q = q.Where(r => r.RequestedAt <= query.To.Value);

        var rows = await q.ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(query.Feature))
            rows = rows.Where(r => r.Feature.Contains(query.Feature.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var id = string.IsNullOrWhiteSpace(query.EmployeeId) ? null : query.EmployeeId.Trim();
        var name = Norm(query.EmployeeName);
        if (id is not null || name is not null)
            rows = rows.Where(r => (id is not null && (string.Equals(r.RequestedById, id, StringComparison.OrdinalIgnoreCase) || string.Equals(r.RequestedByName, id, StringComparison.OrdinalIgnoreCase)))
                || (name is not null && (Norm(r.RequestedByName) == name || Norm(r.RequestedById) == name))).ToList();
        return rows;
    }

    private static List<EaAiUsageTotalsDto> Group(IEnumerable<EaAiUsageLog> rows, Func<EaAiUsageLog, string> key) =>
        rows.GroupBy(key).Select(g => Totals(g.Key, g)).OrderByDescending(t => t.TotalTokens).ThenByDescending(t => t.Calls).ToList();

    private static EaAiUsageTotalsDto Totals(string key, IEnumerable<EaAiUsageLog> source)
    {
        var rows = source.ToList();
        return new EaAiUsageTotalsDto
        {
            Key = key,
            Calls = rows.Count,
            Succeeded = rows.Count(r => r.Status == "Succeeded"),
            InvalidJson = rows.Count(r => r.Status == "InvalidJson"),
            Failed = rows.Count(r => r.Status == "Failed"),
            Used = rows.Count(r => r.IsUsed),
            NotUsed = rows.Count(r => r.Status == "Succeeded" && !r.IsUsed),
            InputTokens = rows.Sum(r => r.InputTokens ?? 0),
            OutputTokens = rows.Sum(r => r.OutputTokens ?? 0),
            TotalTokens = rows.Sum(r => r.TotalTokens ?? 0),
            LastUsedAt = rows.Count == 0 ? null : rows.Max(r => r.RequestedAt),
        };
    }

    private static T Row<T>(T dto, EaAiUsageLog r) where T : EaAiUsageRowDto
    {
        dto.Id = r.Id; dto.Module = r.Module; dto.Feature = r.Feature; dto.Endpoint = r.Endpoint;
        dto.BusinessRecordId = r.BusinessRecordId; dto.EaTaskId = r.EaTaskId;
        dto.RequestedById = r.RequestedById; dto.RequestedByName = r.RequestedByName; dto.RequestedAt = r.RequestedAt;
        dto.DurationMs = r.DurationMs; dto.AttemptNo = r.AttemptNo; dto.Status = r.Status; dto.Model = r.Model;
        dto.InputTokens = r.InputTokens; dto.OutputTokens = r.OutputTokens; dto.TotalTokens = r.TotalTokens;
        dto.IsUsed = r.IsUsed; dto.UsedAt = r.UsedAt; dto.UsedByName = r.UsedByName;
        return dto;
    }

    private static string? Allowed(string? value, string[] allowed, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return allowed.FirstOrDefault(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BadRequestException($"{field} must be one of: {string.Join(", ", allowed)}.");
    }

    private static string? Norm(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : string.Join(' ', v.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
