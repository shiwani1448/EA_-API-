namespace Jarvis5.Repositories.EaFms;

public interface IDelegationNumberRepository
{
    Task<string> GenerateNextReferenceNoAsync(CancellationToken ct = default);
}
