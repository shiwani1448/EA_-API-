using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using hrms_api.DTOs;

namespace hrms_api.Services;

public interface IOnboardingFileService
{
    Task<OnboardingSubmissionFileDto> SaveAsync(int candidateId, int assessmentId, string taskId, IFormFile file, CancellationToken ct);
    bool IsValidReference(int candidateId, int assessmentId, string taskId, OnboardingSubmissionFileDto file);
}

public interface IOnboardingEvidenceProcessor
{
    Task<OnboardingProcessedEvidence> ProcessAsync(int candidateId, int assessmentId, JsonDocument responses, CancellationToken ct);
}
public sealed record OnboardingProcessedEvidence(List<object> Tasks, int FilesReviewed, bool ManualReviewRequired);

public sealed class OnboardingFileService : IOnboardingFileService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"]=["image/png"], [".jpg"]=["image/jpeg"], [".jpeg"]=["image/jpeg"], [".webp"]=["image/webp"],
        [".pdf"]=["application/pdf"], [".doc"]=["application/msword"],
        [".docx"]=["application/vnd.openxmlformats-officedocument.wordprocessingml.document"], [".txt"]=["text/plain"],
        [".xls"]=["application/vnd.ms-excel"], [".xlsx"]=["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [".mp4"]=["video/mp4"], [".mov"]=["video/quicktime"], [".webm"]=["video/webm"],
        [".dwg"]=["application/acad","application/octet-stream"], [".rvt"]=["application/octet-stream"],
        [".skp"]=["application/octet-stream"], [".psd"]=["image/vnd.adobe.photoshop","application/octet-stream"],
        [".3ds"]=["application/octet-stream"]
    };
    public OnboardingFileService(IWebHostEnvironment env, IConfiguration configuration) { _env = env; _configuration = configuration; }

    public async Task<OnboardingSubmissionFileDto> SaveAsync(int candidateId, int assessmentId, string taskId, IFormFile file, CancellationToken ct)
    {
        if (file.Length <= 0) throw new ArgumentException("Uploaded file is empty.");
        var ext = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
        if (!Allowed.TryGetValue(ext, out var mimeTypes) || !mimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("File extension or MIME type is not supported.");
        var maxMb = ext is ".mp4" or ".mov" or ".webm" ? _configuration.GetValue("OnboardingFiles:MaxVideoSizeMb", 100) : _configuration.GetValue("OnboardingFiles:MaxFileSizeMb", 25);
        if (file.Length > maxMb * 1024L * 1024L) throw new ArgumentException($"File exceeds the {maxMb} MB limit.");
        var fileId = Guid.NewGuid().ToString("N");
        var safeTask = new string(taskId.Where(char.IsLetterOrDigit).ToArray());
        if (safeTask.Length == 0) throw new ArgumentException("Invalid task ID.");
        var relativeDir = Path.Combine("uploads", "onboarding", candidateId.ToString(), assessmentId.ToString(), safeTask);
        var root = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var directory = Path.GetFullPath(Path.Combine(root, relativeDir)); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileId + ext);
        if (!await HasValidSignatureAsync(file, ext, ct)) throw new ArgumentException("File content does not match its extension.");
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) await file.CopyToAsync(stream, ct);
        return new OnboardingSubmissionFileDto { FileId = fileId, FileName = Path.GetFileName(file.FileName), MimeType = file.ContentType, Size = file.Length,
            Url = "/" + Path.Combine(relativeDir, fileId + ext).Replace('\\', '/') };
    }

    public bool IsValidReference(int candidateId, int assessmentId, string taskId, OnboardingSubmissionFileDto file)
    {
        if (!Guid.TryParseExact(file.FileId, "N", out _)) return false;
        var safeTask = new string(taskId.Where(char.IsLetterOrDigit).ToArray());
        var root = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var allowed = Path.GetFullPath(Path.Combine(root, "uploads", "onboarding", candidateId.ToString(), assessmentId.ToString(), safeTask));
        if (string.IsNullOrWhiteSpace(file.Url)) return false;
        var full = Path.GetFullPath(Path.Combine(root, file.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
               Path.GetFileNameWithoutExtension(full).Equals(file.FileId, StringComparison.OrdinalIgnoreCase) && File.Exists(full);
    }

    private static async Task<bool> HasValidSignatureAsync(IFormFile file, string ext, CancellationToken ct)
    {
        if (ext is ".txt" or ".dwg" or ".rvt" or ".skp" or ".psd" or ".3ds") return true;
        var header = new byte[16]; await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(), ct);
        bool Starts(params byte[] bytes) => read >= bytes.Length && header.AsSpan(0, bytes.Length).SequenceEqual(bytes);
        return ext switch
        {
            ".pdf" => Starts(0x25,0x50,0x44,0x46),
            ".png" => Starts(0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A),
            ".jpg" or ".jpeg" => Starts(0xFF,0xD8,0xFF),
            ".webp" => read >= 12 && Encoding.ASCII.GetString(header,0,4)=="RIFF" && Encoding.ASCII.GetString(header,8,4)=="WEBP",
            ".docx" or ".xlsx" => Starts(0x50,0x4B),
            ".doc" or ".xls" => Starts(0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1),
            ".mp4" or ".mov" => read >= 8 && Encoding.ASCII.GetString(header,4,4)=="ftyp",
            ".webm" => Starts(0x1A,0x45,0xDF,0xA3),
            _ => false
        };
    }
}

public sealed class OnboardingEvidenceProcessor : IOnboardingEvidenceProcessor
{
    private readonly IWebHostEnvironment _env; private readonly IDocumentExtractionService _documents;
    public OnboardingEvidenceProcessor(IWebHostEnvironment env, IDocumentExtractionService documents) { _env = env; _documents = documents; }
    public async Task<OnboardingProcessedEvidence> ProcessAsync(int candidateId, int assessmentId, JsonDocument responses, CancellationToken ct)
    {
        var evidence = new List<object>(); var filesReviewed = 0; var manual = false;
        foreach (var day in responses.RootElement.GetProperty("days").EnumerateArray())
        foreach (var task in day.GetProperty("tasks").EnumerateArray())
        {
            var taskId = task.GetProperty("taskId").GetString();
            var text = task.TryGetProperty("textResponse", out var tr) && tr.ValueKind == JsonValueKind.String ? tr.GetString() : null;
            var processedFiles = new List<object>();
            if (task.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
            foreach (var file in files.EnumerateArray())
            {
                filesReviewed++; var url = file.TryGetProperty("url", out var u) ? u.GetString() : null;
                var ext = Path.GetExtension(url ?? "").ToLowerInvariant(); string? extracted = null; var status = "MANUAL_REVIEW_REQUIRED";
                if (!string.IsNullOrWhiteSpace(url))
                {
                    var root = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var full = Path.GetFullPath(Path.Combine(root, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                    var allowedRoot = Path.GetFullPath(Path.Combine(root, "uploads", "onboarding", candidateId.ToString(), assessmentId.ToString()));
                    if (full.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                    {
                        if (ext is ".pdf" or ".doc" or ".docx" or ".txt") { var result = await _documents.ExtractAsync(full, ct); extracted = result.Text; status = result.Success ? "AI_EVALUATED" : "PARTIALLY_EVALUATED"; }
                        else if (ext == ".xlsx") { extracted = ExtractWorkbook(full); status = "AI_EVALUATED"; }
                        else if (ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".mp4" or ".mov" or ".webm" or ".dwg" or ".rvt" or ".skp" or ".psd" or ".3ds") manual = true;
                    }
                }
                processedFiles.Add(new { fileId = file.GetProperty("fileId").GetString(), fileName = file.GetProperty("fileName").GetString(), processingStatus = status, extractedContent = extracted });
            }
            evidence.Add(new { day = day.GetProperty("day").GetInt32(), taskId, textResponse = text, files = processedFiles });
        }
        return new OnboardingProcessedEvidence(evidence, filesReviewed, manual);
    }
    private static string ExtractWorkbook(string path)
    {
        using var doc = SpreadsheetDocument.Open(path, false); var sb = new StringBuilder();
        var workbook = doc.WorkbookPart;
        var shared = workbook?.SharedStringTablePart?.SharedStringTable;
        foreach (var sheet in workbook?.WorksheetParts ?? []) foreach (var row in sheet.Worksheet?.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>() ?? [])
        { foreach (var cell in row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>()) { var value = cell.CellValue?.Text ?? ""; if (cell.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString && int.TryParse(value, out var i)) value = shared?.ElementAtOrDefault(i)?.InnerText ?? value; sb.Append(value).Append('\t'); } sb.AppendLine(); }
        return sb.ToString();
    }
}
