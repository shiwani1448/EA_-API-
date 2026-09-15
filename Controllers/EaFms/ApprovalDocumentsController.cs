using Jarvis5.Dtos.EaFms;
using Jarvis5.Entities.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

[ApiController]
[Route("api/ea/approvals/{approvalRequestId:long}/documents")]
public class ApprovalDocumentsController : ControllerBase
{
    private readonly IApprovalDocumentService _docs;

    public ApprovalDocumentsController(IApprovalDocumentService docs) => _docs = docs;

    [HttpPost]
    public async Task<IActionResult> Upload(long approvalRequestId, IFormFile file, [FromForm] long? approvalCycleId, CancellationToken ct)
    {
        var result = await _docs.UploadAsync(approvalRequestId, file, approvalCycleId, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(long approvalRequestId, CancellationToken ct)
    {
        var result = await _docs.ListAsync(approvalRequestId, ct);
        return Ok(result);
    }

    [HttpGet("{documentId:long}")]
    public async Task<IActionResult> Get(long approvalRequestId, long documentId, CancellationToken ct)
    {
        var (content, contentType, fileName) = await _docs.DownloadAsync(approvalRequestId, documentId, ct);
        return File(content, contentType, fileName);
    }

    [HttpDelete("{documentId:long}")]
    public async Task<IActionResult> Delete(long approvalRequestId, long documentId, CancellationToken ct)
    {
        await _docs.DeleteAsync(approvalRequestId, documentId, ct);
        return NoContent();
    }
}
