using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class JDMastersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public JDMastersController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<JDMasterResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<JDMasterResponseDto>>>> GetAll()
    {
        var jdMasters = await _db.JDMasters
            .Where(j => !j.IsDeleted)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<JDMasterResponseDto>>.Ok(
            jdMasters.Select(MapToResponse).ToList(),
            "JD masters fetched successfully."));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JDMasterResponseDto>>> GetById(int id)
    {
        var jdMaster = await _db.JDMasters
            .FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);

        if (jdMaster is null)
            return NotFound(ApiResponse<JDMasterResponseDto>.Fail("JD master not found."));

        return Ok(ApiResponse<JDMasterResponseDto>.Ok(MapToResponse(jdMaster), "JD master fetched successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<JDMasterResponseDto>>> Create([FromBody] JDMasterDto dto)
    {
        if (!TryDecodeBase64File(dto.DocumentPdf, out var documentBytes))
            return BadRequest(ApiResponse<JDMasterResponseDto>.Fail("DocumentPdf must be valid Base64."));

        var documentPath = documentBytes is not null
            ? await SaveDocumentPdfAsync(documentBytes, dto.DocumentPdfFileName)
            : null;

        var jdMaster = MapToEntity(dto, documentPath);
        jdMaster.CreatedAt = DateTime.UtcNow;
        jdMaster.UpdatedAt = null;
        jdMaster.IsDeleted = false;

        try
        {
            _db.JDMasters.Add(jdMaster);
            await _db.SaveChangesAsync();
        }
        catch
        {
            DeleteDocumentPdfFile(documentPath);
            throw;
        }

        return CreatedAtAction(nameof(GetById), new { id = jdMaster.Id },
            ApiResponse<JDMasterResponseDto>.Ok(MapToResponse(jdMaster), "JD master created successfully."));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JDMasterResponseDto>>> Update(int id, [FromBody] JDMasterDto dto)
    {
        var jdMaster = await _db.JDMasters
            .FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);

        if (jdMaster is null)
            return NotFound(ApiResponse<JDMasterResponseDto>.Fail("JD master not found."));

        if (!TryDecodeBase64File(dto.DocumentPdf, out var documentBytes))
            return BadRequest(ApiResponse<JDMasterResponseDto>.Fail("DocumentPdf must be valid Base64."));

        jdMaster.Department = dto.Department;
        jdMaster.Designation = dto.Designation;
        jdMaster.JobDescription = dto.JobDescription;
        jdMaster.SkillsRequired = dto.SkillsRequired;
        jdMaster.Qualification = dto.Qualification;
        jdMaster.Experience = dto.Experience;
        if (documentBytes is not null)
        {
            DeleteDocumentPdfFile(jdMaster.DocumentPdfPath);
            jdMaster.DocumentPdfPath = await SaveDocumentPdfAsync(documentBytes, dto.DocumentPdfFileName);
            jdMaster.DocumentPdfFileName = SafeFileName(dto.DocumentPdfFileName);
            jdMaster.DocumentPdfContentType = dto.DocumentPdfContentType;
        }
        jdMaster.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<JDMasterResponseDto>.Ok(MapToResponse(jdMaster), "JD master updated successfully."));
    }

    [HttpPost("{id:int}/documentpdf")]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JDMasterResponseDto>>> UploadDocumentPdf(
        int id,
        [FromBody] JDDocumentUploadDto dto)
    {
        var jdMaster = await _db.JDMasters
            .FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);

        if (jdMaster is null)
            return NotFound(ApiResponse<JDMasterResponseDto>.Fail("JD master not found."));

        if (!TryDecodeBase64File(dto.DocumentPdf, out var documentBytes) || documentBytes is null)
            return BadRequest(ApiResponse<JDMasterResponseDto>.Fail("DocumentPdf must be valid Base64."));

        DeleteDocumentPdfFile(jdMaster.DocumentPdfPath);
        jdMaster.DocumentPdfPath = await SaveDocumentPdfAsync(documentBytes, dto.DocumentPdfFileName);
        jdMaster.DocumentPdfFileName = SafeFileName(dto.DocumentPdfFileName);
        jdMaster.DocumentPdfContentType = dto.DocumentPdfContentType;
        jdMaster.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<JDMasterResponseDto>.Ok(MapToResponse(jdMaster), "JD document uploaded successfully."));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JDMasterResponseDto>>> Delete(int id)
    {
        var jdMaster = await _db.JDMasters
            .FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);

        if (jdMaster is null)
            return NotFound(ApiResponse<JDMasterResponseDto>.Fail("JD master not found."));

        jdMaster.IsDeleted = true;
        jdMaster.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<JDMasterResponseDto>.Ok(MapToResponse(jdMaster), "JD master deleted successfully."));
    }

    private static JDMaster MapToEntity(JDMasterDto dto, string? documentPath) => new()
    {
        Department = dto.Department,
        Designation = dto.Designation,
        JobDescription = dto.JobDescription,
        SkillsRequired = dto.SkillsRequired,
        Qualification = dto.Qualification,
        Experience = dto.Experience,
        DocumentPdfPath = documentPath,
        DocumentPdfFileName = SafeFileName(dto.DocumentPdfFileName),
        DocumentPdfContentType = dto.DocumentPdfContentType
    };

    private static JDMasterResponseDto MapToResponse(JDMaster jdMaster) => new()
    {
        Id = jdMaster.Id,
        Department = jdMaster.Department,
        Designation = jdMaster.Designation,
        JobDescription = jdMaster.JobDescription,
        SkillsRequired = jdMaster.SkillsRequired,
        Qualification = jdMaster.Qualification,
        Experience = jdMaster.Experience,
        DocumentPdfPath = jdMaster.DocumentPdfPath,
        DocumentPdfFileName = jdMaster.DocumentPdfFileName,
        DocumentPdfContentType = jdMaster.DocumentPdfContentType,
        CreatedAt = jdMaster.CreatedAt,
        UpdatedAt = jdMaster.UpdatedAt
    };

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

    private async Task<string> SaveDocumentPdfAsync(byte[] bytes, string? fileName)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads", "jd-documents");

        Directory.CreateDirectory(uploadsDir);

        var safeFile = SafeFileName(fileName) ?? "jd-document.pdf";
        var uniqueName = $"{Guid.NewGuid()}_{safeFile}";
        var filePath = Path.Combine(uploadsDir, uniqueName);

        await System.IO.File.WriteAllBytesAsync(filePath, bytes);

        return $"/uploads/jd-documents/{uniqueName}";
    }

    private void DeleteDocumentPdfFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullPath = Path.Combine(wwwroot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
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
}
