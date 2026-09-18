using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class DelegationRepository : IDelegationNumberRepository
{
    private readonly EaFmsDbContext _context;

    public DelegationRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextReferenceNoAsync(CancellationToken ct = default)
    {
        // Use the ea_delegation_no_seq sequence defined in EaFmsDbContext
        var next = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('ea_delegation_no_seq') AS \"Value\"")
            .SingleAsync(ct);

        var year = DateTime.UtcNow.Year;
        return $"DLG-{year}-{next:D6}";
    }
}
