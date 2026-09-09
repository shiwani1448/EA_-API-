using System.Text.Json;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SourcingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SourcingsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<Sourcing>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<Sourcing>>>> GetAll()
    {
        var sourcings = await _db.Sourcings
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Sourcing>>.Ok(sourcings, "Sourcing records fetched successfully."));
    }

    [HttpGet("{sourcingId:int}")]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Sourcing>>> GetById(int sourcingId)
    {
        var sourcing = await _db.Sourcings
            .FirstOrDefaultAsync(s => s.SourcingId == sourcingId && !s.IsDeleted);

        if (sourcing is null)
            return NotFound(ApiResponse<Sourcing>.Fail("Sourcing record not found."));

        return Ok(ApiResponse<Sourcing>.Ok(sourcing, "Sourcing record fetched successfully."));
    }

    [HttpGet("hiringrequest/{hiringRequestId:int}")]
    [ProducesResponseType(typeof(ApiResponse<List<Sourcing>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<Sourcing>>>> GetByHiringRequestId(int hiringRequestId)
    {
        var sourcings = await _db.Sourcings
            .Where(s => s.HiringRequestId == hiringRequestId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<Sourcing>>.Ok(sourcings, "Sourcing records fetched successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<Sourcing>>> Create([FromBody] SourcingDto dto)
    {
        if (!await HiringRequestExists(dto.HiringRequestId))
            return BadRequest(ApiResponse<Sourcing>.Fail("Hiring request not found."));

        var sourcing = MapToEntity(dto);
        sourcing.CreatedAt = DateTime.UtcNow;
        sourcing.UpdatedAt = null;
        sourcing.IsDeleted = false;

        _db.Sourcings.Add(sourcing);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { sourcingId = sourcing.SourcingId },
            ApiResponse<Sourcing>.Ok(sourcing, "Sourcing record created successfully."));
    }

    [HttpPut("{sourcingId:int}")]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Sourcing>>> Update(int sourcingId, [FromBody] SourcingDto dto)
    {
        var sourcing = await _db.Sourcings
            .FirstOrDefaultAsync(s => s.SourcingId == sourcingId && !s.IsDeleted);

        if (sourcing is null)
            return NotFound(ApiResponse<Sourcing>.Fail("Sourcing record not found."));

        if (!await HiringRequestExists(dto.HiringRequestId))
            return BadRequest(ApiResponse<Sourcing>.Fail("Hiring request not found."));

        sourcing.HiringRequestId = dto.HiringRequestId;
        sourcing.Source = dto.Source;
        sourcing.SubSource = dto.SubSource;
        sourcing.TaskDetails = CloneTaskDetails(dto.TaskDetails);
        sourcing.Status = NormalizeStatus(dto.Status);
        sourcing.StartTime = NormalizeUtc(dto.StartTime);
        sourcing.EndTime = NormalizeUtc(dto.EndTime);
        sourcing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<Sourcing>.Ok(sourcing, "Sourcing record updated successfully."));
    }

    [HttpDelete("{sourcingId:int}")]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<Sourcing>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<Sourcing>>> Delete(int sourcingId)
    {
        var sourcing = await _db.Sourcings
            .FirstOrDefaultAsync(s => s.SourcingId == sourcingId && !s.IsDeleted);

        if (sourcing is null)
            return NotFound(ApiResponse<Sourcing>.Fail("Sourcing record not found."));

        sourcing.IsDeleted = true;
        sourcing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<Sourcing>.Ok(sourcing, "Sourcing record deleted successfully."));
    }

    private async Task<bool> HiringRequestExists(int hiringRequestId) =>
        await _db.HiringRequests.AnyAsync(h => h.RequestId == hiringRequestId && !h.IsDeleted);

    private static Sourcing MapToEntity(SourcingDto dto) => new()
    {
        HiringRequestId = dto.HiringRequestId,
        Source = dto.Source,
        SubSource = dto.SubSource,
        TaskDetails = CloneTaskDetails(dto.TaskDetails),
        Status = NormalizeStatus(dto.Status),
        StartTime = NormalizeUtc(dto.StartTime),
        EndTime = NormalizeUtc(dto.EndTime)
    };

    private static JsonDocument? CloneTaskDetails(JsonElement? taskDetails) =>
        taskDetails.HasValue
            ? JsonDocument.Parse(taskDetails.Value.GetRawText())
            : null;

    private static string NormalizeStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ? "Pending" : status.Trim();

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value?.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => value
        };
}
