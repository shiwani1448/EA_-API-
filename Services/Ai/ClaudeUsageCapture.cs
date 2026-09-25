namespace Jarvis5.Services.Ai;

/// <summary>Model and token usage of one Claude call, filled in by ClaudeClient while it streams.</summary>
public sealed class ClaudeCallUsage
{
    public string? Model { get; set; }
    public string? MessageId { get; set; }
    public string? StopReason { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? CacheCreationInputTokens { get; set; }
    public long? CacheReadInputTokens { get; set; }
}

/// <summary>
/// Opt-in capture of Claude usage for the current async flow. A caller that wants the usage
/// (the EA FMS AI usage logger) calls Begin() before the Claude call; ClaudeClient fills the
/// returned object. Callers that never call Begin() are unaffected.
/// </summary>
public static class ClaudeUsageCapture
{
    private static readonly AsyncLocal<ClaudeCallUsage?> CurrentUsage = new();

    public static ClaudeCallUsage? Current => CurrentUsage.Value;

    public static ClaudeCallUsage Begin()
    {
        var usage = new ClaudeCallUsage();
        CurrentUsage.Value = usage;
        return usage;
    }

    public static void End() => CurrentUsage.Value = null;
}
