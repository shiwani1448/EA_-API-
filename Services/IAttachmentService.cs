using Jarvis5.Dtos.Attachment;

namespace Jarvis5.Services;

public interface IAttachmentService
{
    /// <summary>Saves every file in the request to disk and inserts one
    /// SCIH_Attachments row per file.</summary>
    Task<List<AttachmentResponseDto>> UploadAsync(UploadAttachmentRequestDto dto, CancellationToken ct = default);

    /// <summary>All non-deleted attachments for a given module.</summary>
    Task<List<AttachmentResponseDto>> GetByEntityAsync(string entityType, long entityId, CancellationToken ct = default);

    /// <summary>Soft deletes an attachment (IsDeleted = true). The physical file is
    /// kept on disk.</summary>
    Task DeleteAsync(long attachmentId, CancellationToken ct = default);

    /// <summary>Reads the physical file for an attachment, for the download endpoint.</summary>
    Task<(byte[] Content, string ContentType, string FileName)> DownloadAsync(long attachmentId, CancellationToken ct = default);
}
