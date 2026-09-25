using Jarvis5.Dtos.EaFms;
using Microsoft.AspNetCore.Http;

namespace Jarvis5.Services.EaFms;

public interface ITravelDocumentService
{
    Task<TravelDocumentResponseDto> UploadAsync(long travelRequestId, IFormFile file, string? documentCategory, string? employeeId = null, string? employeeName = null, CancellationToken ct = default);
    Task<List<TravelDocumentResponseDto>> ListAsync(long travelRequestId, CancellationToken ct = default);
    Task<(byte[] Content, string ContentType, string FileName)> DownloadAsync(long documentId, CancellationToken ct = default);
    Task DeleteAsync(long documentId, CancellationToken ct = default);
}
