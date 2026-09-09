namespace Jarvis5.Services;

/// <summary>Writes attachment-module files to disk under Content/{EntityType}/{EntityId}/.
/// Kept separate from IAttachmentFileService (which the existing Request/Solution-Design
/// inline-attachment flows use) so this module stays fully independent of them.</summary>
public interface IAttachmentStorageService
{
    /// <summary>Decodes <paramref name="base64Content"/>, writes it under
    /// Content/{entityType}/{entityId}/, and returns the generated file name, its
    /// FileUrl (relative to the web root) and its size in bytes.</summary>
    (string FileName, string FileUrl, long FileSize) Save(string entityType, long entityId, string originalName, string base64Content);

    /// <summary>Resolves a stored FileUrl (e.g. "/Content/Request/15/x.pdf") back to an
    /// absolute path on disk, for the download endpoint to read.</summary>
    string ResolvePhysicalPath(string fileUrl);
}
