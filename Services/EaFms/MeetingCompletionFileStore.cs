using Jarvis5.Common;
using UglyToad.PdfPig;
namespace Jarvis5.Services.EaFms;

public interface IMeetingCompletionFileStore
{
    Task<byte[]> ValidateAsync(IFormFile file, CancellationToken ct);
    Task<string> SaveAsync(long meetingId, byte[] content, CancellationToken ct);
    void Delete(string objectKey);

    /// <summary>Resolves a stored completion-PDF object key to its safe, contained
    /// physical path for reading (e.g. AI text extraction). Same containment rules
    /// as writes — throws if the key resolves outside the completion storage root.</summary>
    string Resolve(string objectKey);
}

// Uses the existing local Content storage convention, with an isolated completion folder.
// Owns the object key before writing so even partial writes can be cleaned up safely.
public class MeetingCompletionFileStore(IWebHostEnvironment environment) : IMeetingCompletionFileStore
{
    public const long MaxBytes = 25 * 1024 * 1024;
    public async Task<byte[]> ValidateAsync(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0 || file.Length > MaxBytes)
            throw new BusinessRuleException("A nonempty completion PDF of at most 25 MiB is required.");
        if (string.IsNullOrWhiteSpace(file.FileName) || Path.GetFileName(file.FileName).Length > 500)
            throw new BusinessRuleException("Completion PDF filename must contain 1 to 500 characters.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Completion file must be a PDF.");
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > MaxBytes) throw new BusinessRuleException("Completion PDF exceeds 25 MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        var bytes = buffer.ToArray();
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new BusinessRuleException("Completion PDF signature is invalid.");
        try { using var pdf = PdfDocument.Open(bytes); if (pdf.NumberOfPages < 1) throw new InvalidDataException(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { throw new BusinessRuleException("Completion file is not a readable PDF."); }
        return bytes;
    }
    public async Task<string> SaveAsync(long meetingId, byte[] content, CancellationToken ct)
    {
        var key = $"Content/MeetingCompletion/{meetingId}/{Guid.NewGuid():N}.pdf";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            { await stream.WriteAsync(content, ct); await stream.FlushAsync(ct); stream.Flush(true); }
            return key;
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    public void Delete(string objectKey) { var path = Resolve(objectKey); if (File.Exists(path)) File.Delete(path); }
    public string Resolve(string key)
    {
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "Content", "MeetingCompletion")) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid completion storage key.");
        return path;
    }
}
