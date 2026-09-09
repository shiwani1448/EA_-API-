namespace hrms_api.Services;

public interface IAiScreeningPromptBuilder
{
    string BuildPrompt(string jdText, string resumeText);
    AiScreeningPromptBuildResult BuildCompressedPrompt(string jdText, string resumeText);
}

public sealed record AiScreeningPromptBuildResult(
    string Prompt,
    string CompressedJDText,
    string CompressedResumeText);
