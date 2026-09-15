using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class ApprovalRepository : IApprovalNumberRepository
{
    private readonly EaFmsDbContext _context;

    public ApprovalRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextReferenceNoAsync(CancellationToken ct = default)
    {
        // Use the ea_approval_no_seq sequence defined in EaFmsDbContext
        var next = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('ea_approval_no_seq') AS \"Value\"")
            .SingleAsync(ct);

        var year = DateTime.UtcNow.Year;
        return $"APR-{year}-{next:D6}";
    }
}
