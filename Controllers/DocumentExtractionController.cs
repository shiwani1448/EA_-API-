using hrms_api.DTOs;
using hrms_api.Services;
using Microsoft.AspNetCore.Mvc;

namespace hrms_api.Controllers;

[ApiController]
[Route("document-extraction")]
[Produces("application/json")]
public class DocumentExtractionController : ControllerBase
{
    private readonly IDocumentExtractionService _documentExtractionService;
    private readonly IOcrDependencyHealthService _ocrDependencyHealthService;

    public DocumentExtractionController(
        IDocumentExtractionService documentExtractionService,
        IOcrDependencyHealthService ocrDependencyHealthService)
    {
        _documentExtractionService = documentExtractionService;
        _ocrDependencyHealthService = ocrDependencyHealthService;
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(DocumentExtractionHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentExtractionHealthDto>> Health(CancellationToken cancellationToken)
    {
        return Ok(await _ocrDependencyHealthService.CheckAsync(cancellationToken));
    }

    [HttpPost("test")]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(DocumentExtractionTestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentExtractionTestResponseDto>> Test(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest("PDF file is required.");

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only PDF files are supported by this test endpoint.");

        var tempDir = Path.Combine(Path.GetTempPath(), "hrms-doc-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var safeFileName = Path.GetFileName(file.FileName);
        var fullPath = Path.Combine(tempDir, safeFileName);

        try
        {
            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var result = await _documentExtractionService.ExtractAsync(fullPath, cancellationToken);
            var text = result.Text ?? string.Empty;

            return Ok(new DocumentExtractionTestResponseDto
            {
                IsSelectableTextPdf = result.IsSelectableTextPdf,
                NormalExtractedTextLength = result.NormalTextLength,
                IsImageBasedPdf = result.IsImageBasedPdf,
                PageCount = result.PdfPageCount,
                OCRExtractedTextLength = result.OCRTextLength,
                First1000Characters = text.Length <= 1000 ? text : text[..1000],
                ExtractionMethod = result.ExtractionMethodUsed ?? result.ExtractionMethod,
                FailureStep = result.FailureStep,
                FailureReason = result.FailureReason
            });
        }
        finally
        {
            try { Directory.Delete(tempDir, true); }
            catch { }
        }
    }
}
