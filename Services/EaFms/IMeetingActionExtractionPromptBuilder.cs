namespace Jarvis5.Services.EaFms;

public interface IMeetingActionExtractionPromptBuilder
{
    string BuildSystemPrompt();

    /// <summary>At least one of <paramref name="mom"/>/<paramref name="pdfText"/> is
    /// expected to be non-null — the caller guarantees there is usable evidence before
    /// building the prompt.</summary>
    string BuildUserPrompt(string? mom, string? pdfText);
}
