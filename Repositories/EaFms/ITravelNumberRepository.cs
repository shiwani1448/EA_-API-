namespace Jarvis5.Repositories.EaFms;

public interface ITravelNumberRepository
{
    Task<string> GenerateNextReferenceNoAsync(CancellationToken ct = default);
}
