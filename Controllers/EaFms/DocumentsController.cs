using Jarvis5.Common;
using Jarvis5.Dtos.EaFms;
using Jarvis5.Services.EaFms;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers.EaFms;

/// <summary>
/// Central Document &amp; Records: one register over the documents uploaded by every EA module (read-only) and one central download.
/// </summary>
[ApiController]
[Route("api/ea/documents")]
public class DocumentsController(IDocumentRegisterService service) : ControllerBase
{
    /// <summary>
    /// Lists real ea_attachments rows (active, not deleted) of the Meeting, Delegation, Travel and Approval uploads, newest first,
    /// with the canonical module, normalized type, central EaTask and a central download link. Read-only; never exposes the storage path.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DocumentRegisterRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] DocumentRegisterQueryDto query, CancellationToken ct) =>
        Ok(await service.ListAsync(query, ct));

    /// <summary>Opens/downloads a registered document regardless of its source module. 404 when the row is missing/inactive/deleted or the file is unavailable.</summary>
    [HttpGet("{attachmentId:long}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(long attachmentId, CancellationToken ct)
    {
        var file = await service.DownloadAsync(attachmentId, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
