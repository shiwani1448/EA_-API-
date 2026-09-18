using Jarvis5.Data.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class TravelRepository : ITravelNumberRepository
{
    private readonly EaFmsDbContext _context;

    public TravelRepository(EaFmsDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextReferenceNoAsync(CancellationToken ct = default)
    {
        // Use the ea_travel_no_seq sequence defined in EaFmsDbContext
        var next = await _context.Database
            .SqlQueryRaw<long>("SELECT nextval('ea_travel_no_seq') AS \"Value\"")
            .SingleAsync(ct);

        var year = DateTime.UtcNow.Year;
        return $"TRV-{year}-{next:D6}";
    }
}
