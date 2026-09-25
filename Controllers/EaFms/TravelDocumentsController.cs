using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

/// <summary>
/// Travel document (Supporting Documents) APIs. Backed entirely by the shared
/// ea_attachments table via TravelDocumentService — no dedicated Travel document table.
///
/// Routes:
///   POST   /api/ea/travel/requests/{travelRequestId}/documents
///   GET    /api/ea/travel/requests/{travelRequestId}/documents
///   GET    /api/ea/travel/documents/{documentId}
///   DELETE /api/ea/travel/documents/{documentId}
/// </summary>
[ApiController]
public class TravelDocumentsController : ControllerBase
{
    private readonly ITravelDocumentService _docs;

    public TravelDocumentsController(ITravelDocumentService docs)
    {
        _docs = docs;
    }

    [HttpPost("api/ea/travel/requests/{travelRequestId:long}/documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        long travelRequestId, IFormFile file, [FromForm] string? documentCategory,
        [FromForm] string? employeeId, [FromForm] string? employeeName, CancellationToken ct)
    {
        var result = await _docs.UploadAsync(travelRequestId, file, documentCategory, employeeId, employeeName, ct);
        return Ok(result);
    }

    [HttpGet("api/ea/travel/requests/{travelRequestId:long}/documents")]
    public async Task<IActionResult> List(long travelRequestId, CancellationToken ct)
    {
        var result = await _docs.ListAsync(travelRequestId, ct);
        return Ok(result);
    }

    [HttpGet("api/ea/travel/documents/{documentId:long}")]
    public async Task<IActionResult> Get(long documentId, CancellationToken ct)
    {
        var (content, contentType, fileName) = await _docs.DownloadAsync(documentId, ct);
        return File(content, contentType, fileName);
    }

    [HttpDelete("api/ea/travel/documents/{documentId:long}")]
    public async Task<IActionResult> Delete(long documentId, CancellationToken ct)
    {
        await _docs.DeleteAsync(documentId, ct);
        return NoContent();
    }
}
