using Jarvis5.Dtos.Analysis;
using Jarvis5.Entities;

namespace Jarvis5.Services.Ai;

public interface IAnalysisPromptBuilder
{
    string BuildSystemPrompt();

    string BuildUserPrompt(SCIHRequest request, List<SimilarRequestDto> similarRequests);
}
