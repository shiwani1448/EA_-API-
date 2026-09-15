using Jarvis5.Data.EaFms;
using Jarvis5.Entities.EaFms;
using Microsoft.EntityFrameworkCore;

namespace Jarvis5.Repositories.EaFms;

public class EaTaskRepository(EaFmsDbContext db) : IEaTaskRepository
{
    public IQueryable<EaTask> Query() => db.Tasks.AsNoTracking()
        .Include(x => x.BusinessModule).Where(x => !x.IsDeleted);
    public async Task AddAsync(EaTask task, CancellationToken ct) => await db.Tasks.AddAsync(task, ct);
}
