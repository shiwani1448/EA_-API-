using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HiringRequestsController : ControllerBase
{
    private readonly AppDbContext _db;

    public HiringRequestsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<HiringRequest>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<HiringRequest>>>> GetAll()
    {
        var requests = await _db.HiringRequests
            .Where(h => !h.IsDeleted)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();

        return Ok(ApiResponse<List<HiringRequest>>.Ok(requests, "Hiring requests fetched successfully."));
    }

    [HttpGet("{requestId:int}")]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HiringRequest>>> GetById(int requestId)
    {
        var request = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == requestId && !h.IsDeleted);

        if (request is null)
            return NotFound(ApiResponse<HiringRequest>.Fail("Hiring request not found."));

        return Ok(ApiResponse<HiringRequest>.Ok(request, "Hiring request fetched successfully."));
    }

    [HttpGet("{requestId:int}/jd")]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<JDMasterResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<JDMasterResponseDto>>> GetJobDescription(int requestId)
    {
        var request = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == requestId && !h.IsDeleted);

        if (request is null)
            return NotFound(ApiResponse<JDMasterResponseDto>.Fail("Hiring request not found."));

        if (request.JDID is null)
            return NotFound(ApiResponse<JDMasterResponseDto>.Fail("No JD is linked to this hiring request."));

        var jdMaster = await _db.JDMasters
            .FirstOrDefaultAsync(j => j.Id == request.JDID && !j.IsDeleted);

        if (jdMaster is null)
            return NotFound(ApiResponse<JDMasterResponseDto>.Fail("JD master not found."));

        return Ok(ApiResponse<JDMasterResponseDto>.Ok(MapJdToResponse(jdMaster), "JD fetched successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<HiringRequest>>> Create([FromBody] HiringRequestDto dto)
    {
        var request = await MapToEntity(dto);
        request.CreatedAt = DateTime.UtcNow;
        request.UpdatedAt = null;
        request.IsDeleted = false;
        request.HrStatus = "Pending";

        _db.HiringRequests.Add(request);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { requestId = request.RequestId },
            ApiResponse<HiringRequest>.Ok(request, "Hiring request created successfully."));
    }

    [HttpPut("{requestId:int}")]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HiringRequest>>> Update(int requestId, [FromBody] HiringRequestDto dto)
    {
        var request = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == requestId && !h.IsDeleted);

        if (request is null)
            return NotFound(ApiResponse<HiringRequest>.Fail("Hiring request not found."));

        request.JDID = await GetMatchingJDID(dto.Department, dto.Designation);
        request.Department = dto.Department;
        request.Designation = dto.Designation;
        request.NumberOfPosition = dto.NumberOfPosition;
        request.Priority = dto.Priority;
        request.RequiredByDate = NormalizeUtc(dto.RequiredByDate);
        request.ExperienceRequired = dto.ExperienceRequired;
        request.ReasonForHiring = dto.ReasonForHiring;
        request.RequestById = dto.RequestById;
        request.IsApprovedByDirector = dto.IsApprovedByDirector;
        request.DirectorName = dto.DirectorName;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<HiringRequest>.Ok(request, "Hiring request updated successfully."));
    }

    [HttpPatch("{requestId:int}/hrstatus")]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HiringRequest>>> UpdateHrStatus(
        int requestId,
        [FromBody] HiringRequestHrStatusDto dto)
    {
        var request = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == requestId && !h.IsDeleted);

        if (request is null)
            return NotFound(ApiResponse<HiringRequest>.Fail("Hiring request not found."));

        var hrStatus = string.IsNullOrWhiteSpace(dto.HrStatus)
            ? "Pending"
            : dto.HrStatus.Trim();
        request.HrStatus = hrStatus;
        request.HrAcceptDate = IsAcceptedStatus(hrStatus)
            ? DateTime.UtcNow
            : request.HrAcceptDate;
        request.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<HiringRequest>.Ok(request, "HR status updated successfully."));
    }

    [HttpPatch("{requestId:int}/director-approval")]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HiringRequest>>> UpdateDirectorApproval(
        int requestId,
        [FromBody] HiringRequestDirectorApprovalDto dto)
    {
        var request = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == requestId && !h.IsDeleted);

        if (request is null)
            return NotFound(ApiResponse<HiringRequest>.Fail("Hiring request not found."));

        var now = DateTime.UtcNow;
        request.IsApprovedByDirector = dto.IsApprovedByDirector;
        request.DirectorStatus = dto.IsApprovedByDirector switch
        {
            true => "Approved",
            false => "Rejected",
            null => "Pending"
        };
        request.DirectorActionDate = now;
        request.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<HiringRequest>.Ok(request, "Director approval updated successfully."));
    }

    [HttpPatch("{requestId:int}/director-status")]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HiringRequest>>> UpdateDirectorStatus(
        int requestId,
        [FromBody] HiringRequestDirectorStatusDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DirectorStatus))
            return BadRequest(ApiResponse<HiringRequest>.Fail("DirectorStatus is required."));

        var request = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == requestId && !h.IsDeleted);

        if (request is null)
            return NotFound(ApiResponse<HiringRequest>.Fail("Hiring request not found."));

        var status = dto.DirectorStatus.Trim();
        var now = DateTime.UtcNow;

        request.DirectorStatus = status;
        request.DirectorActionDate = now;
        request.DirectorName = string.IsNullOrWhiteSpace(dto.DirectorName)
            ? request.DirectorName
            : dto.DirectorName.Trim();
        request.IsApprovedByDirector = DirectorApprovalFromStatus(status) ?? request.IsApprovedByDirector;
        request.UpdatedAt = now;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<HiringRequest>.Ok(request, "Director status updated successfully."));
    }
    [HttpDelete("{requestId:int}")]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HiringRequest>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<HiringRequest>>> Delete(int requestId)
    {
        var request = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == requestId && !h.IsDeleted);

        if (request is null)
            return NotFound(ApiResponse<HiringRequest>.Fail("Hiring request not found."));

        request.IsDeleted = true;
        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<HiringRequest>.Ok(request, "Hiring request deleted successfully."));
    }

    private async Task<HiringRequest> MapToEntity(HiringRequestDto dto) => new()
    {
        JDID = await GetMatchingJDID(dto.Department, dto.Designation),
        Department = dto.Department,
        Designation = dto.Designation,
        NumberOfPosition = dto.NumberOfPosition,
        Priority = dto.Priority,
        RequiredByDate = NormalizeUtc(dto.RequiredByDate),
        ExperienceRequired = dto.ExperienceRequired,
        ReasonForHiring = dto.ReasonForHiring,
        RequestById = dto.RequestById,
        IsApprovedByDirector = dto.IsApprovedByDirector,
        DirectorName = dto.DirectorName
    };

    private async Task<int?> GetMatchingJDID(string? department, string? designation)
    {
        if (string.IsNullOrWhiteSpace(department) || string.IsNullOrWhiteSpace(designation))
            return null;

        var normalizedDepartment = department.Trim().ToLower();
        var normalizedDesignation = designation.Trim().ToLower();

        return await _db.JDMasters
            .Where(j => !j.IsDeleted)
            .Where(j => j.Department != null && j.Designation != null)
            .Where(j => j.Department!.Trim().ToLower() == normalizedDepartment &&
                        j.Designation!.Trim().ToLower() == normalizedDesignation)
            .Select(j => (int?)j.Id)
            .FirstOrDefaultAsync();
    }

    private static JDMasterResponseDto MapJdToResponse(JDMaster jdMaster) => new()
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

    private static bool IsAcceptedStatus(string hrStatus) =>
        string.Equals(hrStatus, "Accept", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(hrStatus, "Accepted", StringComparison.OrdinalIgnoreCase);

    private static bool? DirectorApprovalFromStatus(string status)
    {
        if (status.Equals("Approve", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Accept", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Accepted", StringComparison.OrdinalIgnoreCase))
            return true;

        if (status.Equals("Reject", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Decline", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Declined", StringComparison.OrdinalIgnoreCase))
            return false;

        return null;
    }
    private static DateTime? NormalizeUtc(DateTime? value) =>
        value?.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => value
        };
}


