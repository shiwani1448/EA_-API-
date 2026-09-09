using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CallRecordingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CallRecordingsController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>
    /// Upload a call recording (Base64-encoded). Returns the stored file path on success.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ApiResponse<CallRecordingResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CallRecordingResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CallRecordingResponseDto>>> Upload(
        [FromBody] CallRecordingUploadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FileBase64))
            return BadRequest(ApiResponse<CallRecordingResponseDto>.Fail("FileBase64 is required."));

        if (string.IsNullOrWhiteSpace(dto.FileName))
            return BadRequest(ApiResponse<CallRecordingResponseDto>.Fail("FileName is required."));

        if (!TryDecodeBase64(dto.FileBase64, out var fileBytes) || fileBytes is null)
            return BadRequest(ApiResponse<CallRecordingResponseDto>.Fail("FileBase64 must be valid Base64 data."));

        var storedPath = await SaveRecordingAsync(fileBytes, dto.FileName);

        var recording = new CallRecording
        {
            CandidateId          = dto.CandidateId,
            RecordingFileName    = SafeFileName(dto.FileName),
            RecordingPath        = storedPath,
            RecordingContentType = dto.ContentType,
            DurationSeconds      = dto.DurationSeconds,
            CallerName           = dto.CallerName,
            Notes                = dto.Notes,
            RecordedAt           = dto.RecordedAt?.ToUniversalTime() ?? DateTime.UtcNow,
            CreatedAt            = DateTime.UtcNow,
            IsDeleted            = false
        };

        try
        {
            _db.CallRecordings.Add(recording);
            await _db.SaveChangesAsync();
        }
        catch
        {
            DeleteFile(storedPath);
            throw;
        }

        return CreatedAtAction(nameof(GetById), new { id = recording.RecordingId },
            ApiResponse<CallRecordingResponseDto>.Ok(MapToResponse(recording), "Call recording uploaded successfully."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CallRecordingResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CallRecordingResponseDto>>>> GetAll(
        [FromQuery] int? candidateId)
    {
        var query = _db.CallRecordings.Where(r => !r.IsDeleted);

        if (candidateId.HasValue)
            query = query.Where(r => r.CandidateId == candidateId.Value);

        var recordings = await query
            .OrderByDescending(r => r.RecordedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<CallRecordingResponseDto>>.Ok(
            recordings.Select(MapToResponse).ToList(),
            "Call recordings fetched successfully."));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CallRecordingResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CallRecordingResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CallRecordingResponseDto>>> GetById(int id)
    {
        var recording = await _db.CallRecordings
            .FirstOrDefaultAsync(r => r.RecordingId == id && !r.IsDeleted);

        if (recording is null)
            return NotFound(ApiResponse<CallRecordingResponseDto>.Fail("Recording not found."));

        return Ok(ApiResponse<CallRecordingResponseDto>.Ok(MapToResponse(recording), "Recording fetched successfully."));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CallRecordingResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CallRecordingResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CallRecordingResponseDto>>> Delete(int id)
    {
        var recording = await _db.CallRecordings
            .FirstOrDefaultAsync(r => r.RecordingId == id && !r.IsDeleted);

        if (recording is null)
            return NotFound(ApiResponse<CallRecordingResponseDto>.Fail("Recording not found."));

        recording.IsDeleted = true;
        recording.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<CallRecordingResponseDto>.Ok(MapToResponse(recording), "Recording deleted successfully."));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> SaveRecordingAsync(byte[] bytes, string? fileName)
    {
        var uploadsDir = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads", "call-recordings");

        Directory.CreateDirectory(uploadsDir);

        var safeFile  = SafeFileName(fileName) ?? "recording";
        var uniqueName = $"{Guid.NewGuid()}_{safeFile}";
        var filePath   = Path.Combine(uploadsDir, uniqueName);

        await System.IO.File.WriteAllBytesAsync(filePath, bytes);

        return $"/uploads/call-recordings/{uniqueName}";
    }

    private void DeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var wwwroot  = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullPath = Path.Combine(wwwroot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }

    private static bool TryDecodeBase64(string value, out byte[]? bytes)
    {
        bytes = null;
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
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var safeName = Path.GetFileName(fileName.Trim());
        foreach (var ch in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(ch, '_');

        return safeName;
    }

    private static CallRecordingResponseDto MapToResponse(CallRecording r) => new()
    {
        RecordingId          = r.RecordingId,
        CandidateId          = r.CandidateId,
        RecordingFileName    = r.RecordingFileName,
        RecordingPath        = r.RecordingPath,
        RecordingContentType = r.RecordingContentType,
        DurationSeconds      = r.DurationSeconds,
        CallerName           = r.CallerName,
        Notes                = r.Notes,
        RecordedAt           = r.RecordedAt,
        CreatedAt            = r.CreatedAt
    };
}
