using Jarvis5.Common;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;
using Jarvis5.Repositories;

namespace Jarvis5.Services;

/// <summary>Searches company knowledge (previous solution designs) for the request
/// most similar to the current one, so Solution Design generation can reuse existing
/// modules/APIs/tables/UI/SOP/flowcharts instead of designing duplicates. Reuses the
/// same Title/Pain-Points/Expected-Benefit/Department similarity scoring already
/// built for Analysis, then narrows to requests that have a saved solution design.</summary>
public class SolutionKnowledgeFinder : ISolutionKnowledgeFinder
{
    private const int CandidatePoolMultiplier = 5;

    private readonly ISimilarRequestFinder _similarRequestFinder;
    private readonly ISolutionDesignRepository _solutionDesignRepository;

    public SolutionKnowledgeFinder(ISimilarRequestFinder similarRequestFinder, ISolutionDesignRepository solutionDesignRepository)
    {
        _similarRequestFinder = similarRequestFinder;
        _solutionDesignRepository = solutionDesignRepository;
    }

    public async Task<List<PreviousSolutionDesignDto>> FindTopReusableAsync(SCIHRequest request, int topN = 5, CancellationToken ct = default)
    {
        var candidates = await _similarRequestFinder.FindTopSimilarAsync(request, topN: topN * CandidatePoolMultiplier, ct: ct);
        if (candidates.Count == 0) return new List<PreviousSolutionDesignDto>();

        var designs = await _solutionDesignRepository.GetByRequestIdsAsync(
            candidates.Select(c => c.RequestId).ToList(), ct);
        if (designs.Count == 0) return new List<PreviousSolutionDesignDto>();

        return candidates
            .Where(c => designs.ContainsKey(c.RequestId))
            .Select(c => (Candidate: c, Design: designs[c.RequestId]))
            .OrderByDescending(x => x.Candidate.SimilarityScore + (x.Design.Status == SCIHSolutionDesignStatus.Approved ? 25 : 0))
            .Take(topN)
            .Select(x =>
            {
                var solution = JsonHelper.DeserializeObjectOrDefault<SolutionDesignResultDto>(x.Design.SolutionJson);
                return new PreviousSolutionDesignDto
                {
                    RequestId = x.Candidate.RequestId,
                    RequestNo = x.Candidate.RequestNo,
                    Title = x.Candidate.Title,
                    DepartmentId = x.Candidate.DepartmentId,
                    Status = x.Design.Status,
                    SimilarityScore = x.Candidate.SimilarityScore,
                    SolutionTitle = solution.SolutionOverview.Title,
                    SolutionDescription = solution.SolutionOverview.Description,
                    ModuleNames = solution.ModuleBreakdown.Select(m => m.Module).ToList(),
                    ReusableApis = solution.ReusableAssets.Apis,
                    ReusableDatabaseTables = solution.ReusableAssets.DatabaseTables,
                    ReusableModules = solution.ReusableAssets.Modules,
                    ReusableUiComponents = solution.ReusableAssets.UiComponents,
                    ReusableFlowcharts = solution.ReusableAssets.Flowcharts,
                    ReusableDocuments = solution.ReusableAssets.Documents
                };
            })
            .ToList();
    }
}
