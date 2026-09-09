using Jarvis5.Dtos;

namespace Jarvis5.Services;

public interface IAttachmentFileService
{
    /// <summary>If the attachment carries raw base64 content from the frontend, decodes
    /// it, writes the file to the Content folder, and derives FileName/FileUrl/FileSize
    /// from what was actually written. Attachments that already reference an existing
    /// FileUrl (no Base64Content) pass through unchanged.</summary>
    AttachmentDto Persist(AttachmentDto item);
}
