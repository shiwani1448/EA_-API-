using Jarvis5.Dtos.Analysis;
using Jarvis5.Entities;

namespace Jarvis5.Services;

public interface ISimilarRequestFinder
{
    /// <summary>Top N (default 5) previous requests most similar to <paramref name="request"/>,
    /// scored on Title/Pain Points/Expected Benefit/Department overlap.</summary>
    Task<List<SimilarRequestDto>> FindTopSimilarAsync(SCIHRequest request, int topN = 5, CancellationToken ct = default);
}
