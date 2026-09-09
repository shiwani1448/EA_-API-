using System.Text.RegularExpressions;
using Jarvis5.Common;
using Jarvis5.Dtos;
using Jarvis5.Dtos.Analysis;
using Jarvis5.Entities;
using Jarvis5.Repositories;

namespace Jarvis5.Services;

/// <summary>Ranks previous requests by keyword overlap with the current request's
/// Title/Pain Points/Expected Benefit/Department, so the AI gets organisation-specific
/// context instead of a generic answer. No external search engine required.</summary>
public class SimilarRequestFinder : ISimilarRequestFinder
{
    private static readonly Regex WordSplitter = new(@"[^a-zA-Z0-9]+", RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "for", "of", "to", "in", "on", "is", "are", "with",
        "this", "that", "we", "it", "be", "as", "by", "at", "our", "not", "no", "have", "has"
    };

    private readonly IRequestRepository _requestRepository;
    private readonly IAnalysisRepository _analysisRepository;

    public SimilarRequestFinder(IRequestRepository requestRepository, IAnalysisRepository analysisRepository)
    {
        _requestRepository = requestRepository;
        _analysisRepository = analysisRepository;
    }

    public async Task<List<SimilarRequestDto>> FindTopSimilarAsync(SCIHRequest request, int topN = 5, CancellationToken ct = default)
    {
        var candidates = await _requestRepository.GetSimilarityCandidatesAsync(request.Id, maxCandidates: 500, ct);
        if (candidates.Count == 0) return new List<SimilarRequestDto>();

        var currentPainPoints = JsonHelper.DeserializeList<PainPointDto>(request.PainPointsJson);
        var currentBag = BuildWordBag(request.Title, request.ExpectedBenefit, currentPainPoints);

        var scored = candidates
            .Select(c =>
            {
                var painPoints = JsonHelper.DeserializeList<PainPointDto>(c.PainPointsJson);
                var bag = BuildWordBag(c.Title, c.ExpectedBenefit, painPoints);
                var score = JaccardSimilarity(currentBag, bag);

                if (string.Equals(c.DepartmentId, request.DepartmentId, StringComparison.OrdinalIgnoreCase))
                    score += 0.15;

                return new
                {
                    Candidate = c,
                    PainPointTitles = painPoints.Select(p => p.Title).ToList(),
                    Score = score
                };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topN)
            .ToList();

        if (scored.Count == 0) return new List<SimilarRequestDto>();

        var summaries = await _analysisRepository.GetExecutiveSummariesAsync(
            scored.Select(x => x.Candidate.Id).ToList(), ct);

        return scored.Select(x => new SimilarRequestDto
        {
            RequestId = x.Candidate.Id,
            RequestNo = x.Candidate.RequestNo,
            Title = x.Candidate.Title,
            DepartmentId = x.Candidate.DepartmentId,
            Priority = x.Candidate.Priority,
            ExpectedBenefit = x.Candidate.ExpectedBenefit,
            PainPointTitles = x.PainPointTitles,
            SimilarityScore = Math.Round(Math.Min(x.Score, 1.0) * 100, 1),
            PreviousAnalysisExecutiveSummary = summaries.TryGetValue(x.Candidate.Id, out var summary) ? summary : null
        }).ToList();
    }

    private static HashSet<string> BuildWordBag(string title, string expectedBenefit, List<PainPointDto> painPoints)
    {
        var text = string.Join(' ', new[] { title, expectedBenefit }
            .Concat(painPoints.Select(p => p.Title))
            .Concat(painPoints.Select(p => p.Description ?? string.Empty)));

        return WordSplitter.Split(text)
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .Select(w => w.ToLowerInvariant())
            .ToHashSet();
    }

    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;

        var intersection = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }
}
