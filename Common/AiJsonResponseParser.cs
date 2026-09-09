using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Jarvis5.Common;

/// <summary>Shared parsing for local-model chat responses that are expected to be a
/// single JSON object: strips any markdown code fences the model added despite
/// being told not to, then deserializes into the expected shape.</summary>
public static class AiJsonResponseParser
{
    private static readonly Regex MarkdownFence = new(@"^\s*```(?:json)?\s*|\s*```\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    public static T Parse<T>(string rawResponse, ILogger logger, string entityName) where T : new()
    {
        var cleaned = MarkdownFence.Replace(rawResponse, string.Empty).Trim();

        try
        {
            return JsonSerializer.Deserialize<T>(cleaned, JsonDefaults.Options)
                ?? throw new BusinessRuleException($"AI {entityName} service returned an empty response.");
        }
        catch (JsonException)
        {
            // Small local models occasionally wrap the JSON in stray prose despite
            // being told not to (e.g. "Here is the analysis:\n{...}\nLet me know..."),
            // or leave trailing garbage after the closing brace. Retry once against
            // just the outermost {...} slice before giving up.
            var firstBrace = cleaned.IndexOf('{');
            var lastBrace = cleaned.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                var extracted = cleaned[firstBrace..(lastBrace + 1)];
                try
                {
                    return JsonSerializer.Deserialize<T>(extracted, JsonDefaults.Options)
                        ?? throw new BusinessRuleException($"AI {entityName} service returned an empty response.");
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "AI {Entity} response could not be parsed as JSON. Raw response: {RawResponse}", entityName, rawResponse);
                    throw new BusinessRuleException(
                        $"AI {entityName} service returned a response that could not be parsed as JSON.");
                }
            }

            logger.LogWarning("AI {Entity} response could not be parsed as JSON. Raw response: {RawResponse}", entityName, rawResponse);
            throw new BusinessRuleException(
                $"AI {entityName} service returned a response that could not be parsed as JSON.");
        }
    }
}
