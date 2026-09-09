using Jarvis5.Common;

namespace Jarvis5.Services;

/// <summary>Registered as a singleton so the filename sequence counter below is shared
/// across every concurrent upload — this is what keeps two files saved in the same
/// millisecond from ever getting the same stored file name (same pattern as the
/// existing AttachmentFileService).</summary>
public class AttachmentStorageService : IAttachmentStorageService
{
    private const string ContentFolderName = "Content";
    private long _fileNameSequence;

    private readonly IWebHostEnvironment _webHostEnvironment;

    public AttachmentStorageService(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public (string FileName, string FileUrl, long FileSize) Save(string entityType, long entityId, string originalName, string base64Content)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Content);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException($"Attachment '{originalName}' has invalid base64 content.");
        }

        var folderName = SCIHAttachmentEntityType.FolderName(entityType);
        var relativeFolder = Path.Combine(ContentFolderName, folderName, entityId.ToString());
        var absoluteFolder = Path.Combine(_webHostEnvironment.ContentRootPath, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var extension = Path.GetExtension(originalName);
        var sequence = Interlocked.Increment(ref _fileNameSequence);
        var storedFileName = $"{entityType.ToUpperInvariant()}_{entityId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{sequence}{extension}";

        File.WriteAllBytes(Path.Combine(absoluteFolder, storedFileName), bytes);

        var fileUrl = $"/{ContentFolderName}/{folderName}/{entityId}/{storedFileName}";
        return (storedFileName, fileUrl, bytes.LongLength);
    }

    public string ResolvePhysicalPath(string fileUrl) =>
        Path.Combine(_webHostEnvironment.ContentRootPath, fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
}
