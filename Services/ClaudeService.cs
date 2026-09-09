using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace hrms_api.Services;

public sealed class ClaudeService : IClaudeService
{
    private const string AnthropicVersion = "2023-06-01";
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ClaudeService> _logger;

    public ClaudeService(HttpClient http, IConfiguration config, ILogger<ClaudeService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default) =>
        SendAsync(prompt, imageBase64: null, cancellationToken);

    public Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default) =>
        SendAsync(prompt + "\n\nReturn only valid JSON. Do not use markdown code fences or add commentary.", null, cancellationToken);

    public Task<string> GenerateJsonAsync(string prompt, object jsonSchema, CancellationToken cancellationToken = default) =>
        SendAsync(prompt + $"\n\nReturn only valid JSON matching this schema exactly. Do not use markdown code fences or add commentary.\n{JsonSerializer.Serialize(jsonSchema)}",
            null, cancellationToken);

    public Task<string> GenerateVisionAsync(string prompt, string imageBase64, CancellationToken cancellationToken = default) =>
        SendAsync(prompt, imageBase64, cancellationToken);

    private async Task<string> SendAsync(string prompt, string? imageBase64, CancellationToken cancellationToken)
    {
        var apiKey = _config["AnthropicSettings:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Anthropic API key is missing. Set AnthropicSettings:ApiKey in appsettings.json.");

        var baseUrl = (_config["AnthropicSettings:BaseUrl"] ?? "https://api.anthropic.com").TrimEnd('/');
        var model = imageBase64 is null
            ? _config["AnthropicSettings:TextModel"] ?? "claude-sonnet-5"
            : _config["AnthropicSettings:VisionModel"] ?? _config["AnthropicSettings:TextModel"] ?? "claude-sonnet-5";
        var timeoutSeconds = _config.GetValue("AnthropicSettings:TimeoutSeconds", 300);
        var maxTokens = _config.GetValue("AnthropicSettings:MaxTokens", 4096);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutSeconds > 0)
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        object content = imageBase64 is null
            ? prompt
            : new object[]
            {
                new { type = "image", source = BuildImageSource(imageBase64) },
                new { type = "text", text = prompt }
            };

        var payload = new
        {
            model,
            max_tokens = maxTokens,
            messages = new[] { new { role = "user", content } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Claude request start model={Model} promptLength={PromptLength} hasImage={HasImage}", model, prompt.Length, imageBase64 is not null);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Claude call timed out after {timeoutSeconds} seconds.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Claude API is unreachable: {ex.Message}", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Claude API returned {(int)response.StatusCode}: {body}");

            using var document = JsonDocument.Parse(body);
            var text = string.Concat(document.RootElement.GetProperty("content").EnumerateArray()
                .Where(block => block.TryGetProperty("type", out var type) && type.GetString() == "text")
                .Select(block => block.GetProperty("text").GetString()));

            stopwatch.Stop();
            _logger.LogInformation("Claude request end model={Model} durationMs={DurationMs} responseChars={Length}", model, stopwatch.ElapsedMilliseconds, text.Length);
            return text;
        }
    }

    private static object BuildImageSource(string imageBase64)
    {
        var data = imageBase64;
        var mediaType = imageBase64.StartsWith("iVBOR", StringComparison.Ordinal) ? "image/png"
            : imageBase64.StartsWith("R0lGOD", StringComparison.Ordinal) ? "image/gif"
            : imageBase64.StartsWith("UklGR", StringComparison.Ordinal) ? "image/webp"
            : "image/jpeg";
        if (imageBase64.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var separator = imageBase64.IndexOf(',');
            var metadata = separator >= 0 ? imageBase64[5..separator] : string.Empty;
            mediaType = metadata.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? mediaType;
            data = separator >= 0 ? imageBase64[(separator + 1)..] : imageBase64;
        }

        return new { type = "base64", media_type = mediaType, data };
    }
}



