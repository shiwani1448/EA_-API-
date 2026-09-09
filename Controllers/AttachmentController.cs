using Jarvis5.Dtos.Attachment;
using Jarvis5.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jarvis5.Controllers;

/// <summary>Shared attachment module for Request Raised, Solution Design, and
/// Development Plan. The frontend calls this separately, after the respective
/// module has already been saved, passing that module's EntityType/EntityId.</summary>
[ApiController]
[Route("api/attachments")]
public class AttachmentController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;

    public AttachmentController(IAttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    /// <summary>Uploads one or more files (base64) for a module and stores their
    /// metadata in SCIH_Attachments.</summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(List<AttachmentResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AttachmentResponseDto>>> Upload([FromBody] UploadAttachmentRequestDto dto, CancellationToken ct)
    {
        var result = await _attachmentService.UploadAsync(dto, ct);
        return Ok(result);
    }

    /// <summary>All non-deleted attachments for a given module.</summary>
    [HttpGet("{entityType}/{entityId:long}")]
    [ProducesResponseType(typeof(List<AttachmentResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AttachmentResponseDto>>> GetByEntity(string entityType, long entityId, CancellationToken ct)
    {
        var result = await _attachmentService.GetByEntityAsync(entityType, entityId, ct);
        return Ok(result);
    }

    /// <summary>Soft deletes an attachment. The physical file is kept on disk.</summary>
    [HttpDelete("{attachmentId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(long attachmentId, CancellationToken ct)
    {
        await _attachmentService.DeleteAsync(attachmentId, ct);
        return NoContent();
    }

    /// <summary>Downloads the physical file for an attachment.</summary>
    [HttpGet("download/{attachmentId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(long attachmentId, CancellationToken ct)
    {
        var (content, contentType, fileName) = await _attachmentService.DownloadAsync(attachmentId, ct);
        return File(content, contentType, fileName);
    }
}
