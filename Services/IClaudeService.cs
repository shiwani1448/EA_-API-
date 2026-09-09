namespace hrms_api.Services;

public interface IClaudeService
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
    Task<string> GenerateJsonAsync(string prompt, CancellationToken cancellationToken = default);
    Task<string> GenerateJsonAsync(string prompt, object jsonSchema, CancellationToken cancellationToken = default);
    Task<string> GenerateVisionAsync(string prompt, string imageBase64, CancellationToken cancellationToken = default);
}
