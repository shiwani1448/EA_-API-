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

    // The existing catalog has no Code column. Only this supported module has a known code.
    private static bool IsMeeting(string name) => string.Equals(name.Trim(), "Meeting", StringComparison.OrdinalIgnoreCase);

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
        if (IsMeeting(module.Name))
        {
            result.BusinessModuleCode = "Meeting";
            if (long.TryParse(businessRecordId, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                result.BusinessRecordTitle = await _context.Meetings.AsNoTracking()
                    .Where(x => x.Id == id && !x.IsDeleted).Select(x => x.Title).FirstOrDefaultAsync(ct);
        }
        return result;
    }
}
