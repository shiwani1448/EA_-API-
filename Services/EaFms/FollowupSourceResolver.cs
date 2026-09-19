using System.Globalization;
using Jarvis5.Common;
using Jarvis5.Data.EaFms;
using Jarvis5.Dtos.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Services.EaFms;

public class FollowupSourceResolver : IFollowupSourceResolver
{
    private readonly EaFmsDbContext _context;
    public FollowupSourceResolver(EaFmsDbContext context) => _context = context;

    // Module names as they appear in ea_business_modules. Ids are database identities and are
    // never referenced here.
    private const string MeetingModuleName = "Meeting";
    private const string TravelModuleName = TravelRequestService.TravelBusinessModuleName;
    private const string ApprovalModuleName = "EA Approval";
    private const string DelegationModuleName = DelegationService.DelegationBusinessModuleName;

    // The existing catalog has no Code column. Only this supported module has a known code.
    private static bool IsMeeting(string name) => Is(name, MeetingModuleName);
    private static bool Is(string name, string expected) =>
        string.Equals(name.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    public async Task<long> GetMeetingModuleIdAsync(CancellationToken ct = default)
    {
        var modules = await _context.BusinessModules.AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted).ToListAsync(ct);
        var matches = modules.Where(x => IsMeeting(x.Name)).ToList();
        if (matches.Count != 1)
            throw new BadRequestException("Exactly one active Meeting business-module catalog entry is required.");
        return matches[0].Id;
    }

    public async Task<FollowupSourceResponseDto> ResolveAsync(long? businessModuleId, string? businessRecordId, CancellationToken ct = default)
    {
        var result = new FollowupSourceResponseDto();
        if (!businessModuleId.HasValue) return result;
        var module = await _context.BusinessModules.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == businessModuleId && !x.IsDeleted, ct);
        if (module is null) return result;
        result.BusinessModuleName = module.Name;
        if (IsMeeting(module.Name)) result.BusinessModuleCode = "Meeting";
        if (businessRecordId is not null)
        {
            try { result.BusinessRecordTitle = (await LookupAsync(module.Name, businessRecordId, ct))?.Title; }
            catch (BadRequestException) { /* malformed stored id: display only, no title */ }
        }
        return result;
    }

    public async Task<FollowupSourceRecord> ValidateAsync(long businessModuleId, string businessRecordId, CancellationToken ct = default)
    {
        var module = await _context.BusinessModules.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == businessModuleId && x.IsActive && !x.IsDeleted, ct)
            ?? throw new NotFoundException($"Business module {businessModuleId} not found or inactive.");
        var recordId = (businessRecordId ?? string.Empty).Trim();
        if (recordId.Length == 0) throw new BadRequestException("BusinessRecordId must not be blank.");
        return await LookupAsync(module.Name, recordId, ct, module.Id)
            ?? throw new NotFoundException($"{module.Name} record '{recordId}' not found.");
    }

    /// <summary>
    /// One place that knows how each module identifies its source record (the same value its
    /// central EaTask stores as BusinessRecordId). Returns null when the record does not exist;
    /// throws BadRequestException when a numeric-keyed module is given a non-numeric id.
    /// </summary>
    private async Task<FollowupSourceRecord?> LookupAsync(string moduleName, string rawRecordId, CancellationToken ct, long? moduleId = null)
    {
        var recordId = rawRecordId.Trim();
        var name = moduleName.Trim();

        if (Is(name, MeetingModuleName))
        {
            var id = ParseNumericId(name, recordId);
            var m = await _context.Meetings.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)
                .Select(x => new { x.Title, x.IntakeRequestId, x.WorkflowInstanceId }).FirstOrDefaultAsync(ct);
            return m is null ? null : new(name, Canonical(id), m.Title, m.IntakeRequestId, m.WorkflowInstanceId);
        }
        if (Is(name, TravelModuleName))
        {
            var id = ParseNumericId(name, recordId);
            var t = await _context.TravelRequests.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)
                .Select(x => x.ReferenceNo).FirstOrDefaultAsync(ct);
            return t is null ? null : new(name, Canonical(id), t, null, null);
        }
        if (Is(name, DelegationModuleName))
        {
            var id = ParseNumericId(name, recordId);
            var d = await _context.Delegations.AsNoTracking().Where(x => x.Id == id && !x.IsDeleted)
                .Select(x => x.Title).FirstOrDefaultAsync(ct);
            return d is null ? null : new(name, Canonical(id), d, null, null);
        }
        if (Is(name, ApprovalModuleName))
        {
            // Approval's central EaTask (and therefore the workspace) identifies the record by
            // its ReferenceNo, not by the numeric row id.
            var a = await _context.ApprovalRequests.AsNoTracking().Where(x => x.ReferenceNo == recordId && !x.IsDeleted)
                .Select(x => new { x.ReferenceNo, x.RequestTitle }).FirstOrDefaultAsync(ct);
            return a is null ? null : new(name, a.ReferenceNo, a.RequestTitle ?? a.ReferenceNo, null, null);
        }

        // Any other (future) module: its central EaTask is the proof that the record exists.
        var task = await _context.Tasks.AsNoTracking()
            .Where(x => !x.IsDeleted && x.BusinessRecordId == recordId
                && (moduleId.HasValue ? x.BusinessModuleId == moduleId.Value : x.ModuleName == name))
            .Select(x => new { x.Task, x.WorkflowInstanceId }).FirstOrDefaultAsync(ct);
        return task is null ? null : new(name, recordId, task.Task, null, task.WorkflowInstanceId);
    }

    private static long ParseNumericId(string moduleName, string recordId) =>
        long.TryParse(recordId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : throw new BadRequestException($"{moduleName} BusinessRecordId must be a positive integer.");

    private static string Canonical(long id) => id.ToString(CultureInfo.InvariantCulture);
}
