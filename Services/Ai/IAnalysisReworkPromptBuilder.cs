using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;

namespace Jarvis5.Services.Ai;

public interface IAnalysisReworkPromptBuilder
{
    string BuildSystemPrompt();

    string BuildUserPrompt(
        SCIHRequest request,
        AiAnalysisResultDto latestAnalysis,
        int latestAnalysisVersion,
        AiAnalysisResultDto? previousAnalysis,
        SolutionDesignResultDto? previousSolutionDesign,
        SCIHApproval latestApproval,
        List<SimilarRequestDto> similarRequests,
        List<PreviousSolutionDesignDto> previousApprovedSolutions);
}
