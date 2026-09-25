using System.Diagnostics;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.Ai;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

/// <summary>Records every EA FMS AI call in ea_ai_usage_logs.</summary>
public interface IEaAiUsageLogger
{
    /// <summary>Runs one Claude call and records it (success or failure), then returns the response text.</summary>
    Task<string> CallAsync(string module, string feature, string systemPrompt, string userPrompt, int attemptNo,
        Func<Task<string>> call, CancellationToken ct);

    /// <summary>Marks the last recorded call as having returned invalid JSON (the service then retries or fails).</summary>
    Task MarkLastInvalidJsonAsync(string error);

    /// <summary>
    /// The EA used an AI response (applied / confirmed / sent it): marks the latest successful call
    /// of one of these features for this record (or, with no record, her latest one) as used.
    /// </summary>
    Task MarkUsedAsync(string module, string[] features, string? recordId, object? usedValue, CancellationToken ct = default);

    /// <summary>The screen reports that a specific response (the X-AI-Usage-Id it received) was used. False when the id is unknown.</summary>
    Task<bool> MarkUsedByIdAsync(long id, string? usedValue, CancellationToken ct = default);
}

/// <summary>
/// Writes on its own database context so the row is saved even when the surrounding request
/// fails or rolls back, and never interferes with the business context's tracked changes.
/// Logging problems are swallowed: recording must never break an AI feature.
/// </summary>
public sealed class EaAiUsageLogger(
    DbContextOptions<EaFmsDbContext> options,
    ICurrentUserService currentUser,
    IHttpContextAccessor http,
    ILogger<EaAiUsageLogger> logger) : IEaAiUsageLogger
{
    private long? _lastId;

    public async Task<string> CallAsync(string module, string feature, string systemPrompt, string userPrompt, int attemptNo,
        Func<Task<string>> call, CancellationToken ct)
    {
        var usage = ClaudeUsageCapture.Begin();
        var requestedAt = Clock.UtcNowTz;
        var watch = Stopwatch.StartNew();
        string? response = null;
        Exception? error = null;
        try
        {
            response = await call();
            return response;
        }
        catch (Exception ex)
        {
            error = ex;
            throw;
        }
        finally
        {
            watch.Stop();
            ClaudeUsageCapture.End();
            await WriteAsync(module, feature, systemPrompt, userPrompt, attemptNo, requestedAt, (int)watch.ElapsedMilliseconds,
                response, error, usage);
        }
    }

    public async Task MarkLastInvalidJsonAsync(string error)
    {
        if (_lastId is not { } id) return;
        try
        {
            await using var db = new EaFmsDbContext(options);
            var row = await db.AiUsageLogs.SingleOrDefaultAsync(r => r.Id == id);
            if (row is null) return;
            row.Status = "InvalidJson";
            row.ErrorMessage = error;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not mark AI usage log {Id} as invalid JSON.", id);
        }
    }

    private async Task WriteAsync(string module, string feature, string systemPrompt, string userPrompt, int attemptNo,
        DateTime requestedAt, int durationMs, string? response, Exception? error, ClaudeCallUsage usage)
    {
        try
        {
            var context = http.HttpContext;
            var recordId = RecordIdFromRoute(context);
            var (byId, byName) = WhoIsCalling(context);

            await using var db = new EaFmsDbContext(options);
            var row = new EaAiUsageLog
            {
                Module = module,
                Feature = feature,
                Endpoint = context is null ? null : $"{context.Request.Method} {context.Request.Path}",
                BusinessRecordId = recordId,
                EaTaskId = await ResolveEaTaskIdAsync(db, module, recordId),
                RequestedById = byId,
                RequestedByName = byName,
                RequestedAt = requestedAt,
                CompletedAt = Clock.UtcNowTz,
                DurationMs = durationMs,
                AttemptNo = attemptNo,
                Status = error is null ? "Succeeded" : "Failed",
                ErrorMessage = error?.Message,
                Model = usage.Model,
                MessageId = usage.MessageId,
                StopReason = usage.StopReason,
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                ResponseText = response,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                CacheCreationInputTokens = usage.CacheCreationInputTokens,
                CacheReadInputTokens = usage.CacheReadInputTokens,
                TotalTokens = usage.InputTokens is null && usage.OutputTokens is null ? null
                    : (usage.InputTokens ?? 0) + (usage.OutputTokens ?? 0) + (usage.CacheCreationInputTokens ?? 0) + (usage.CacheReadInputTokens ?? 0),
                CreatedDate = Clock.UtcNowTz,
            };
            db.AiUsageLogs.Add(row);
            await db.SaveChangesAsync();
            _lastId = row.Id;
            // The screen gets the row id with the AI response, so it can report "used" later.
            if (context is not null && !context.Response.HasStarted)
                context.Response.Headers["X-AI-Usage-Id"] = row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _lastId = null;
            logger.LogWarning(ex, "Could not record EA AI usage for {Module} / {Feature}.", module, feature);
        }
    }

    public async Task MarkUsedAsync(string module, string[] features, string? recordId, object? usedValue, CancellationToken ct = default)
    {
        try
        {
            var (byId, byName) = WhoIsCalling(http.HttpContext);
            await using var db = new EaFmsDbContext(options);
            var q = db.AiUsageLogs.Where(r => r.Module == module && features.Contains(r.Feature) && r.Status == "Succeeded");
            q = recordId is not null
                ? q.Where(r => r.BusinessRecordId == recordId)
                : q.Where(r => r.BusinessRecordId == null && (byId == null && byName == null || r.RequestedById == byId || r.RequestedByName == byName));
            var row = await q.OrderByDescending(r => r.RequestedAt).ThenByDescending(r => r.Id).FirstOrDefaultAsync(ct);
            if (row is null) return;   // she used the feature without asking AI first
            Use(row, byId, byName, usedValue is null ? null : System.Text.Json.JsonSerializer.Serialize(usedValue, JsonWeb));
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not mark EA AI usage as used for {Module} / {RecordId}.", module, recordId);
        }
    }

    public async Task<bool> MarkUsedByIdAsync(long id, string? usedValue, CancellationToken ct = default)
    {
        var (byId, byName) = WhoIsCalling(http.HttpContext);
        await using var db = new EaFmsDbContext(options);
        var row = await db.AiUsageLogs.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (row is null) return false;
        Use(row, byId, byName, string.IsNullOrWhiteSpace(usedValue) ? row.UsedValue : usedValue.Trim());
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void Use(EaAiUsageLog row, string? byId, string? byName, string? usedValue)
    {
        row.IsUsed = true;
        row.UsedAt = Clock.UtcNowTz;
        row.UsedById = byId;
        row.UsedByName = byName;
        row.UsedValue = usedValue;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonWeb = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>The record the AI was used for: the first numeric "…Id" route value (e.g. delegationId, meetingId).</summary>
    private static string? RecordIdFromRoute(HttpContext? context)
    {
        if (context is null) return null;
        foreach (var (key, value) in context.Request.RouteValues)
            if (key.EndsWith("id", StringComparison.OrdinalIgnoreCase) && long.TryParse(value?.ToString(), out var id))
                return id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return null;
    }

    /// <summary>The login first; otherwise the EA identity the frontend sends (headers, then query string).</summary>
    private (string? Id, string? Name) WhoIsCalling(HttpContext? context)
    {
        var id = currentUser.ActorId();
        var name = currentUser.ActorName();
        if (context is not null && id is null && name is null)
        {
            id = Clean(context.Request.Headers["X-Employee-Id"].FirstOrDefault()) ?? Clean(context.Request.Query["employeeId"].FirstOrDefault());
            name = Clean(context.Request.Headers["X-Employee-Name"].FirstOrDefault()) ?? Clean(context.Request.Query["employeeName"].FirstOrDefault());
        }
        return (id, name);
    }

    private static async Task<long?> ResolveEaTaskIdAsync(EaFmsDbContext db, string module, string? recordId)
    {
        if (!long.TryParse(recordId, out var id)) return null;
        return module switch
        {
            EaAiModules.Delegation => await db.Delegations.Where(d => d.Id == id).Select(d => (long?)d.EaTaskId).FirstOrDefaultAsync(),
            EaAiModules.Approval => await db.ApprovalRequests.Where(a => a.Id == id).Select(a => (long?)a.EaTaskId).FirstOrDefaultAsync(),
            EaAiModules.Travel => await db.TravelRequests.Where(t => t.Id == id).Select(t => (long?)t.EaTaskId).FirstOrDefaultAsync(),
            EaAiModules.Followup => await db.Followups.Where(f => f.Id == id).Select(f => f.EaTaskId).FirstOrDefaultAsync(),
            EaAiModules.Meeting => await db.Tasks.Where(t => t.ModuleName == "Meeting" && t.BusinessRecordId == recordId && !t.IsDeleted)
                .OrderByDescending(t => t.Id).Select(t => (long?)t.Id).FirstOrDefaultAsync(),
            _ => null,
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Called from the "apply / confirm / send" actions of the EA AI features.</summary>
public static class EaAiUsageMarks
{
    /// <summary>Marks the AI response the EA just used. A no-op when the logger is not registered (e.g. unit tests).</summary>
    public static Task MarkAiResponseUsedAsync(this IServiceProvider services, string module, string[] features, long? recordId, object? usedValue, CancellationToken ct) =>
        (services.GetService(typeof(IEaAiUsageLogger)) as IEaAiUsageLogger)?.MarkUsedAsync(module, features,
            recordId?.ToString(System.Globalization.CultureInfo.InvariantCulture), usedValue, ct) ?? Task.CompletedTask;
}

/// <summary>Module names used in ea_ai_usage_logs.</summary>
public static class EaAiModules
{
    public const string Meeting = "Meeting", Delegation = "Delegation", Approval = "Approval", Travel = "Travel",
        Followup = "Follow-up", Calendar = "Calendar";

    public static readonly string[] All = [Meeting, Delegation, Approval, Travel, Followup, Calendar];
}
