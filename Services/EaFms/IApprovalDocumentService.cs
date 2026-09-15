using Microsoft.AspNetCore.Http;

namespace Jarvis5.Services.EaFms;

public interface IApprovalDocumentService
{
    Task<Jarvis5.Dtos.EaFms.ApprovalDocumentResponseDto> UploadAsync(long approvalRequestId, IFormFile file, long? approvalCycleId, CancellationToken ct = default);
    Task<List<Jarvis5.Dtos.EaFms.ApprovalDocumentResponseDto>> ListAsync(long approvalRequestId, CancellationToken ct = default);
    Task<(byte[] Content, string ContentType, string FileName)> DownloadAsync(long approvalRequestId, long documentId, CancellationToken ct = default);
    Task DeleteAsync(long approvalRequestId, long documentId, CancellationToken ct = default);
}
