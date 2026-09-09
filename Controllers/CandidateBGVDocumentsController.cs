using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CandidateBGVDocumentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CandidateBGVDocumentsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CandidateBGVDocumentResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CandidateBGVDocumentResponseDto>>>> GetAll(
        [FromQuery] int? candidateId,
        [FromQuery] string? checkType,
        [FromQuery] string? status)
    {
        var query = _db.CandidateBGVDocuments.AsQueryable();

        if (candidateId.HasValue)
            query = query.Where(d => d.CandidateId == candidateId.Value);

        if (!string.IsNullOrWhiteSpace(checkType))
            query = query.Where(d => d.CheckType == checkType);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.Status == status);

        var documents = await query
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<CandidateBGVDocumentResponseDto>>.Ok(
            documents.Select(MapToResponse).ToList(),
            "Candidate BGV documents fetched successfully."));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateBGVDocumentResponseDto>>> GetById(int id)
    {
        var document = await _db.CandidateBGVDocuments.FirstOrDefaultAsync(d => d.Id == id);

        if (document is null)
            return NotFound(ApiResponse<CandidateBGVDocumentResponseDto>.Fail("Candidate BGV document not found."));

        return Ok(ApiResponse<CandidateBGVDocumentResponseDto>.Ok(
            MapToResponse(document),
            "Candidate BGV document fetched successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CandidateBGVDocumentResponseDto>>> Create(
        [FromBody] CandidateBGVDocumentDto dto)
    {
        if (!TryDecodeBase64File(dto.FileBase64, out var fileBytes))
            return BadRequest(ApiResponse<CandidateBGVDocumentResponseDto>.Fail("FileBase64 must be valid Base64."));

        var filePath = fileBytes is not null
            ? await SaveDocumentAsync(fileBytes, dto.FileName)
            : null;

        var document = new CandidateBGVDocument
        {
            CandidateId = dto.CandidateId,
            CheckType = dto.CheckType,
            DocumentType = dto.DocumentType,
            FilePath = filePath,
            Status = dto.Status,
            Remarks = dto.Remarks,
            UploadedBy = dto.UploadedBy,
            UploadedAt = dto.UploadedAt?.ToUniversalTime() ?? DateTime.UtcNow
        };

        try
        {
            _db.CandidateBGVDocuments.Add(document);
            await _db.SaveChangesAsync();
        }
        catch
        {
            DeleteDocumentFile(filePath);
            throw;
        }

        return CreatedAtAction(nameof(GetById), new { id = document.Id },
            ApiResponse<CandidateBGVDocumentResponseDto>.Ok(
                MapToResponse(document),
                "Candidate BGV document created successfully."));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateBGVDocumentResponseDto>>> Update(
        int id,
        [FromBody] CandidateBGVDocumentDto dto)
    {
        var document = await _db.CandidateBGVDocuments.FirstOrDefaultAsync(d => d.Id == id);

        if (document is null)
            return NotFound(ApiResponse<CandidateBGVDocumentResponseDto>.Fail("Candidate BGV document not found."));

        if (!TryDecodeBase64File(dto.FileBase64, out var fileBytes))
            return BadRequest(ApiResponse<CandidateBGVDocumentResponseDto>.Fail("FileBase64 must be valid Base64."));

        if (fileBytes is not null)
        {
            DeleteDocumentFile(document.FilePath);
            document.FilePath = await SaveDocumentAsync(fileBytes, dto.FileName);
        }

        document.CandidateId = dto.CandidateId;
        document.CheckType = dto.CheckType;
        document.DocumentType = dto.DocumentType;
        document.Status = dto.Status;
        document.Remarks = dto.Remarks;
        document.UploadedBy = dto.UploadedBy;
        document.UploadedAt = dto.UploadedAt?.ToUniversalTime() ?? document.UploadedAt;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<CandidateBGVDocumentResponseDto>.Ok(
            MapToResponse(document),
            "Candidate BGV document updated successfully."));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateBGVDocumentResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateBGVDocumentResponseDto>>> Delete(int id)
    {
        var document = await _db.CandidateBGVDocuments.FirstOrDefaultAsync(d => d.Id == id);

        if (document is null)
            return NotFound(ApiResponse<CandidateBGVDocumentResponseDto>.Fail("Candidate BGV document not found."));

        _db.CandidateBGVDocuments.Remove(document);
        await _db.SaveChangesAsync();
        DeleteDocumentFile(document.FilePath);

        return Ok(ApiResponse<CandidateBGVDocumentResponseDto>.Ok(
            MapToResponse(document),
            "Candidate BGV document deleted successfully."));
    }

    private async Task<string> SaveDocumentAsync(byte[] bytes, string? fileName)
    {
        var uploadsDir = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads", "candidate-bgv-documents");

        Directory.CreateDirectory(uploadsDir);

        var safeFile = SafeFileName(fileName) ?? "document";
        var uniqueName = $"{Guid.NewGuid()}_{safeFile}";
        var filePath = Path.Combine(uploadsDir, uniqueName);

        await System.IO.File.WriteAllBytesAsync(filePath, bytes);

        return $"/uploads/candidate-bgv-documents/{uniqueName}";
    }

    private void DeleteDocumentFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullPath = Path.Combine(wwwroot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }

    private static bool TryDecodeBase64File(string? value, out byte[]? bytes)
    {
        bytes = null;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        var base64 = value.Trim();
        var commaIndex = base64.IndexOf(',');
        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0)
            base64 = base64[(commaIndex + 1)..];

        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? SafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var safeName = Path.GetFileName(fileName.Trim());
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(invalidChar, '_');

        return safeName;
    }

    private static CandidateBGVDocumentResponseDto MapToResponse(CandidateBGVDocument document) => new()
    {
        Id = document.Id,
        CandidateId = document.CandidateId,
        CheckType = document.CheckType,
        DocumentType = document.DocumentType,
        FilePath = document.FilePath,
        Status = document.Status,
        Remarks = document.Remarks,
        UploadedBy = document.UploadedBy,
        UploadedAt = document.UploadedAt
    };
}
