using Jarvis5.Dtos.Analysis;
using Jarvis5.Dtos.SolutionDesign;
using Jarvis5.Entities;

namespace Jarvis5.Services.Ai;

public interface ISolutionDesignPromptBuilder
{
    /// <summary>Identifies which version of this prompt template produced a given
    /// SCIH_SolutionDesign row, so prompt changes over time stay auditable.</summary>
    string PromptVersion { get; }

    string BuildSystemPrompt();

    string BuildUserPrompt(SCIHRequest request, AiAnalysisResultDto analysis, List<PreviousSolutionDesignDto> knowledgeBase);
}
