using System.Text;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Jarvis5.Common;
using Microsoft.Extensions.Options;

namespace Jarvis5.Services.Ai;

/// <summary>Talks to the Anthropic Claude API. Never touches the database — it only
/// turns a prompt into a JSON-shaped text response.</summary>
public class ClaudeClient : IClaudeClient
{
    private readonly AnthropicClient _client;
    private readonly ClaudeOptions _options;

    public ClaudeClient(IOptions<ClaudeOptions> options)
    {
        _options = options.Value;

        var apiKey = _options.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Anthropic API key is missing. Set AnthropicSettings:ApiKey in appsettings.json.");
        }

        _client = new AnthropicClient { ApiKey = apiKey, Timeout = TimeSpan.FromMinutes(30) };
    }

    public async Task<string> GenerateJsonAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var parameters = new MessageCreateParams
        {
            Model = _options.TextModel,
            MaxTokens = _options.MaxTokens,
            System = new List<TextBlockParam> { new() { Text = systemPrompt } },
            Messages = [new() { Role = Role.User, Content = userPrompt }],
        };

        var text = new StringBuilder();
        // Filled only when a caller opted in via ClaudeUsageCapture.Begin() (the EA FMS AI usage log).
        var usage = ClaudeUsageCapture.Current;
        if (usage is not null) usage.Model = _options.TextModel;

        try
        {
            await foreach (var streamEvent in _client.Messages.CreateStreaming(parameters).WithCancellation(ct))
            {
                if (streamEvent.TryPickContentBlockDelta(out var delta) &&
                    delta.Delta.TryPickText(out var textDelta))
                {
                    text.Append(textDelta.Text);
                }
                else if (usage is not null && streamEvent.TryPickStart(out var start))
                {
                    usage.Model = start.Message.Model.ToString().Trim('"');
                    usage.MessageId = start.Message.ID;
                    usage.InputTokens = start.Message.Usage.InputTokens;
                    usage.CacheCreationInputTokens = start.Message.Usage.CacheCreationInputTokens;
                    usage.CacheReadInputTokens = start.Message.Usage.CacheReadInputTokens;
                }
                else if (usage is not null && streamEvent.TryPickDelta(out var messageDelta))
                {
                    usage.OutputTokens = messageDelta.Usage.OutputTokens;
                    usage.StopReason = messageDelta.Delta.StopReason?.ToString().Trim('"');
                }
            }
        }
        catch (AnthropicApiException ex)
        {
            throw new BusinessRuleException(
                $"AI analysis service (Claude) returned an error: {ex.Message}");
        }

        if (text.Length == 0)
        {
            throw new BusinessRuleException("AI analysis service (Claude) returned an empty response.");
        }

        return text.ToString();
    }
}
