namespace Jarvis5.Services.Ai;

public class ClaudeOptions
{
    /// <summary>Anthropic API key loaded from the AnthropicSettings configuration section.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string TextModel { get; set; } = "claude-sonnet-5";

    // Compatibility alias used by the analysis and solution-design services.
    public string Model => TextModel;

    /// <summary>Max tokens Claude will generate per response. Analysis and Solution
    /// Design schemas (multi-section JSON, several nested arrays) need generous headroom
    /// so responses aren't cut off mid-field before the JSON object closes.</summary>
    public int MaxTokens { get; set; } = 16000;

    public int MaxRetries { get; set; } = 2;
}
