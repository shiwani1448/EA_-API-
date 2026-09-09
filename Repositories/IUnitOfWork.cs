namespace Jarvis5.Repositories;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Runs <paramref name="action"/> inside a DB transaction so a request
    /// mutation and its mandatory history entry are always committed together.</summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
}
