namespace Jarvis5.Services.Ai;

public interface IClaudeClient
{
    /// <summary>Sends a system+user prompt to the configured Claude model and returns
    /// the raw response text (expected to be a JSON document).</summary>
    Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
