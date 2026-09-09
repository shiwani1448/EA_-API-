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

        try
        {
            await foreach (var streamEvent in _client.Messages.CreateStreaming(parameters).WithCancellation(ct))
            {
                if (streamEvent.TryPickContentBlockDelta(out var delta) &&
                    delta.Delta.TryPickText(out var textDelta))
                {
                    text.Append(textDelta.Text);
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
