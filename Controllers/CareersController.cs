using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/careers")]
[Produces("application/json")]
public class CareersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CareersController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>POST /api/careers/apply — public endpoint for candidates to apply.</summary>
    [HttpPost("apply")]
    [ProducesResponseType(typeof(ApiResponse<ApplyResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ApplyResponseDto>>> Apply([FromBody] ApplyRequestDto dto)
    {
        var requisitionExists = await _db.HiringRequests
            .AnyAsync(h => h.RequestId == dto.RequisitionId && !h.IsDeleted);
        if (!requisitionExists)
            return NotFound(ApiResponse<ApplyResponseDto>.Fail("Requisition not found."));

        if (!TryDecodeBase64File(dto.ResumeBase64, out var resumeBytes))
            return BadRequest(ApiResponse<ApplyResponseDto>.Fail("ResumeBase64 must be valid Base64."));

        var resumePath = resumeBytes is not null
            ? await SaveResumeAsync(resumeBytes, dto.ResumeFileName)
            : null;

        var now = DateTime.UtcNow;

        try
        {
            var candidate = new Candidate
            {
                RequisitionId    = dto.RequisitionId,
                FullName         = dto.FullName,
                Email            = dto.Email,
                PhoneNumber      = dto.PhoneNumber,
                YearsOfExperience = dto.YearsOfExperience,
                NoticePeriod     = dto.NoticePeriod,
                CurrentCtcLpa    = dto.CurrentCtcLpa,
                ExpectedCtcLpa   = dto.ExpectedCtcLpa,
                KeySkills        = dto.KeySkills,
                Source           = dto.Source,
                ResumePath       = resumePath,
                ResumeFileName   = SafeFileName(dto.ResumeFileName),
                ResumeContentType = dto.ResumeContentType,
                CurrentStage     = "applied",
                CurrentStatus    = "pending",
                CreatedAt        = now,
                IsDeleted        = false
            };

            _db.Candidates.Add(candidate);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Apply), new { },
                ApiResponse<ApplyResponseDto>.Ok(
                    new ApplyResponseDto(candidate.CandidateId, "applied", "pending", now),
                    "Application submitted successfully."));
        }
        catch
        {
            if (resumePath is not null) DeleteResumeFile(resumePath);
            throw;
        }
    }

    private async Task<string> SaveResumeAsync(byte[] bytes, string? fileName)
    {
        var uploadsDir = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads", "resumes");
        Directory.CreateDirectory(uploadsDir);

        var safeFile = SafeFileName(fileName) ?? "resume";
        var uniqueName = $"{Guid.NewGuid()}_{safeFile}";
        await System.IO.File.WriteAllBytesAsync(Path.Combine(uploadsDir, uniqueName), bytes);
        return $"/uploads/resumes/{uniqueName}";
    }

    private void DeleteResumeFile(string relativePath)
    {
        var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullPath = Path.Combine(wwwroot,
            relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
    }

    private static bool TryDecodeBase64File(string? value, out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        var base64 = value.Trim();
        var idx = base64.IndexOf(',');
        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && idx >= 0)
            base64 = base64[(idx + 1)..];
        try { bytes = Convert.FromBase64String(base64); return true; }
        catch (FormatException) { return false; }
    }

    private static string? SafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var safe = Path.GetFileName(fileName.Trim());
        foreach (var c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
        return safe;
    }
}

public record ApplyRequestDto(
    int RequisitionId,
    string? FullName,
    string? Email,
    string? PhoneNumber,
    int? YearsOfExperience,
    string? NoticePeriod,
    decimal? CurrentCtcLpa,
    decimal? ExpectedCtcLpa,
    string? KeySkills,
    string? Source,
    string? ResumeBase64,
    string? ResumeFileName,
    string? ResumeContentType
);

public record ApplyResponseDto(
    int CandidateId,
    string Stage,
    string Status,
    DateTime AppliedAt
);
