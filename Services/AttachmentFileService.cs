using Jarvis5.Common;
using Jarvis5.Dtos;

namespace Jarvis5.Services;

/// <summary>Registered as a singleton so the filename sequence counter below is shared
/// across every caller (Request, Solution Design, and any future stage) writing into the
/// same Content folder — this is what keeps two attachments saved in the same millisecond
/// from ever getting the same stored file name.</summary>
public class AttachmentFileService : IAttachmentFileService
{
    private const string ContentFolderName = "Content";
    private long _fileNameSequence;

    private readonly IWebHostEnvironment _webHostEnvironment;

    public AttachmentFileService(IWebHostEnvironment webHostEnvironment)
    {
        _webHostEnvironment = webHostEnvironment;
    }

    public AttachmentDto Persist(AttachmentDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Base64Content))
            return item;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(item.Base64Content);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException($"Attachment '{item.OriginalName}' has invalid base64 content.");
        }

        var contentFolder = Path.Combine(_webHostEnvironment.ContentRootPath, ContentFolderName);
        Directory.CreateDirectory(contentFolder);

        var extension = Path.GetExtension(item.OriginalName);
        var sequence = Interlocked.Increment(ref _fileNameSequence);
        var storedFileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{sequence}{extension}";
        File.WriteAllBytes(Path.Combine(contentFolder, storedFileName), bytes);

        item.FileName = storedFileName;
        item.FileUrl = $"/{ContentFolderName}/{storedFileName}";
        item.FileSize = bytes.LongLength;
        item.Base64Content = null;

        return item;
    }
}
