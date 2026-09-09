using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;

namespace Jarvis5.Services;

public interface ISolutionKnowledgeFinder
{
    /// <summary>Top N (default 5) previous requests, most similar to <paramref name="request"/>,
    /// that already have a saved solution design — so the AI can reuse existing company
    /// modules/APIs/tables/UI/SOP/flowcharts instead of designing duplicates. Approved
    /// designs are preferred over Draft/Reviewed ones when ranking.</summary>
    Task<List<PreviousSolutionDesignDto>> FindTopReusableAsync(SCIHRequest request, int topN = 5, CancellationToken ct = default);
}
