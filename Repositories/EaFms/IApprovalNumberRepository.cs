using System.Threading;
using System.Threading.Tasks;

namespace Jarvis5.Repositories.EaFms;

public interface IApprovalNumberRepository
{
    Task<string> GenerateNextReferenceNoAsync(CancellationToken ct = default);
}
