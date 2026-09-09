using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CandidatesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CandidatesController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CandidateResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CandidateResponseDto>>>> GetAll()
    {
        var candidates = await _db.Candidates
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        var aiScores = await GetLatestAiScoresAsync(candidates.Select(c => c.CandidateId));

        return Ok(ApiResponse<List<CandidateResponseDto>>.Ok(
            candidates.Select(c => MapToResponse(c, aiScores.GetValueOrDefault(c.CandidateId))).ToList(),
            "Candidates fetched successfully."));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateResponseDto>>> GetById(int id)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateResponseDto>.Fail("Candidate not found."));

        var aiScore = await GetLatestAiScoreAsync(candidate.CandidateId);

        return Ok(ApiResponse<CandidateResponseDto>.Ok(
            MapToResponse(candidate, aiScore),
            "Candidate fetched successfully."));
    }

    [HttpGet("by-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateResponseDto>>> GetByEmail([FromQuery] string email)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.Email != null
                && c.Email.ToLower() == email.Trim().ToLower()
                && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateResponseDto>.Fail("Candidate not found."));

        var aiScore = await GetLatestAiScoreAsync(candidate.CandidateId);
        var response = MapToResponse(candidate, aiScore);

        var onboardingStatus = candidate.CurrentStatus ?? string.Empty;
        var isCandidateOnboarding = candidate.CurrentStage.Equals("Onboarding", StringComparison.OrdinalIgnoreCase)
            && (onboardingStatus.Equals("Stage 1 Completed", StringComparison.OrdinalIgnoreCase)
                || onboardingStatus.Equals("Stage 2 In Progress", StringComparison.OrdinalIgnoreCase)
                || onboardingStatus.Equals("Assessment Submitted", StringComparison.OrdinalIgnoreCase)
                || onboardingStatus.Equals("Assessment Review", StringComparison.OrdinalIgnoreCase));

        if (!isCandidateOnboarding)
        {
            return Ok(ApiResponse<CandidateResponseDto>.Fail(
                "No active onboarding assessment is available for this email address."));
        }

        return Ok(ApiResponse<CandidateResponseDto>.Ok(
            response,
            "Candidate fetched successfully."));
    }

    [HttpGet("by-requisition/{requisitionId:int}")]
    [ProducesResponseType(typeof(ApiResponse<List<CandidateResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CandidateResponseDto>>>> GetByRequisition(int requisitionId)
    {
        var candidates = await _db.Candidates
            .Where(c => c.RequisitionId == requisitionId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        var aiScores = await GetLatestAiScoresAsync(candidates.Select(c => c.CandidateId));

        return Ok(ApiResponse<List<CandidateResponseDto>>.Ok(
            candidates.Select(c => MapToResponse(c, aiScores.GetValueOrDefault(c.CandidateId))).ToList(),
            "Candidates fetched successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateResponseDto>>> Create([FromBody] CandidateDto dto)
    {
        var requisitionExists = await _db.HiringRequests
            .AnyAsync(h => h.RequestId == dto.RequisitionId && !h.IsDeleted);

        if (!requisitionExists)
            return NotFound(ApiResponse<CandidateResponseDto>.Fail("Requisition not found."));

        if (!TryDecodeBase64File(dto.ResumeBase64, out var resumeBytes))
            return BadRequest(ApiResponse<CandidateResponseDto>.Fail("ResumeBase64 must be valid Base64."));

        var resumePath = resumeBytes is not null
            ? await SaveResumeAsync(resumeBytes, dto.ResumeFileName)
            : null;

        var now = DateTime.UtcNow;

        try
        {
            var candidate = new Candidate
            {
                RequisitionId     = dto.RequisitionId,
                FullName          = dto.FullName,
                Email             = dto.Email,
                PhoneNumber       = dto.PhoneNumber,
                YearsOfExperience = dto.YearsOfExperience,
                NoticePeriod      = dto.NoticePeriod,
                CurrentCtcLpa     = dto.CurrentCtcLpa,
                ExpectedCtcLpa    = dto.ExpectedCtcLpa,
                KeySkills         = dto.KeySkills,
                Source            = dto.Source,
                DateOfBirth       = dto.DateOfBirth,
                ResumePath        = resumePath,
                ResumeFileName    = SafeFileName(dto.ResumeFileName),
                ResumeContentType = dto.ResumeContentType,
                CurrentStage      = "applied",
                CurrentStatus     = "pending",
                CreatedAt         = now,
                IsDeleted         = false
            };

            _db.Candidates.Add(candidate);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = candidate.CandidateId },
                ApiResponse<CandidateResponseDto>.Ok(MapToResponse(candidate), "Candidate created successfully."));
        }
        catch
        {
            if (resumePath is not null) DeleteResumeFile(resumePath);
            throw;
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateResponseDto>>> Update(int id, [FromBody] CandidateDto dto)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateResponseDto>.Fail("Candidate not found."));

        var requisitionExists = await _db.HiringRequests
            .AnyAsync(h => h.RequestId == dto.RequisitionId && !h.IsDeleted);

        if (!requisitionExists)
            return NotFound(ApiResponse<CandidateResponseDto>.Fail("Requisition not found."));

        if (!TryDecodeBase64File(dto.ResumeBase64, out var resumeBytes))
            return BadRequest(ApiResponse<CandidateResponseDto>.Fail("ResumeBase64 must be valid Base64."));

        if (resumeBytes is not null)
        {
            DeleteResumeFile(candidate.ResumePath);
            candidate.ResumePath = await SaveResumeAsync(resumeBytes, dto.ResumeFileName);
            candidate.ResumeFileName = SafeFileName(dto.ResumeFileName);
            candidate.ResumeContentType = dto.ResumeContentType;
        }

        candidate.RequisitionId = dto.RequisitionId;
        candidate.FullName = dto.FullName;
        candidate.Email = dto.Email;
        candidate.PhoneNumber = dto.PhoneNumber;
        candidate.YearsOfExperience = dto.YearsOfExperience;
        candidate.NoticePeriod = dto.NoticePeriod;
        candidate.CurrentCtcLpa = dto.CurrentCtcLpa;
        candidate.ExpectedCtcLpa = dto.ExpectedCtcLpa;
        candidate.KeySkills = dto.KeySkills;
        candidate.Source = dto.Source;
        candidate.DateOfBirth = dto.DateOfBirth;
        candidate.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<CandidateResponseDto>.Ok(MapToResponse(candidate), "Candidate updated successfully."));
    }

    [HttpPatch("{id:int}/stage-status")]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateResponseDto>>> UpdateStageStatus(
        int id,
        [FromBody] CandidateStageStatusUpdateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CurrentStage))
            return BadRequest(ApiResponse<CandidateResponseDto>.Fail("CurrentStage is required."));

        if (string.IsNullOrWhiteSpace(dto.CurrentStatus))
            return BadRequest(ApiResponse<CandidateResponseDto>.Fail("CurrentStatus is required."));

        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateResponseDto>.Fail("Candidate not found."));

        var now = DateTime.UtcNow;
        var normalizedStatus = dto.CurrentStatus.Trim();

        candidate.CurrentStage = dto.CurrentStage.Trim();
        candidate.CurrentStatus = normalizedStatus;
        candidate.UpdatedAt = now;

        if (IsShortlistingStatus(normalizedStatus) && candidate.ShortlistingDate is null)
            candidate.ShortlistingDate = now;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<CandidateResponseDto>.Ok(
            MapToResponse(candidate),
            "Candidate stage/status updated successfully."));
    }

    [HttpPost("{id:int}/activity")]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateActivityResponseDto>>> AddActivity(
        int id,
        [FromBody] CandidateActivityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ActivityType))
            return BadRequest(ApiResponse<CandidateActivityResponseDto>.Fail("ActivityType is required."));

        if (string.IsNullOrWhiteSpace(dto.Stage))
            return BadRequest(ApiResponse<CandidateActivityResponseDto>.Fail("Stage is required."));

        if (string.IsNullOrWhiteSpace(dto.Status))
            return BadRequest(ApiResponse<CandidateActivityResponseDto>.Fail("Status is required."));

        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateActivityResponseDto>.Fail("Candidate not found."));

        var actionDate = dto.ActionDate ?? DateTime.UtcNow;
        var activity = new CandidateActivity
        {
            CandidateId = id,
            ActivityType = dto.ActivityType.Trim(),
            Stage = dto.Stage.Trim(),
            Status = dto.Status.Trim(),
            TotalScore = dto.TotalScore,
            EvaluationJson = ConvertToJsonDocument(dto.EvaluationJson),
            Remarks = dto.Remarks,
            ActionDate = actionDate,
            CreatedBy = dto.CreatedBy,
            CreatedAt = DateTime.UtcNow
        };

        candidate.CurrentStage = string.IsNullOrWhiteSpace(dto.NextStage)
            ? activity.Stage
            : dto.NextStage.Trim();
        candidate.CurrentStatus = activity.Status;
        candidate.LastActivityDate = actionDate;
        candidate.NextFollowUpDate = dto.NextFollowUpDate;
        candidate.AssignedHRId = dto.AssignedHRId ?? candidate.AssignedHRId;
        candidate.Remarks = dto.Remarks;
        candidate.UpdatedAt = DateTime.UtcNow;

        _db.CandidateActivities.Add(activity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetActivityById), new { id = candidate.CandidateId, activityId = activity.Id },
            ApiResponse<CandidateActivityResponseDto>.Ok(MapToActivityResponse(activity), "Activity added successfully."));
    }

    [HttpGet("{id:int}/activities")]
    [ProducesResponseType(typeof(ApiResponse<List<CandidateActivityResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<CandidateActivityResponseDto>>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<CandidateActivityResponseDto>>>> GetActivities(int id)
    {
        var candidateExists = await _db.Candidates
            .AnyAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (!candidateExists)
            return NotFound(ApiResponse<List<CandidateActivityResponseDto>>.Fail("Candidate not found."));

        var activities = await _db.CandidateActivities
            .Where(a => a.CandidateId == id)
            .OrderBy(a => a.ActionDate)
            .ToListAsync();

        return Ok(ApiResponse<List<CandidateActivityResponseDto>>.Ok(
            activities.Select(MapToActivityResponse).ToList(),
            "Candidate activities fetched successfully."));
    }

    [HttpGet("{id:int}/activities/{activityId:int}")]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateActivityResponseDto>>> GetActivityById(int id, int activityId)
    {
        var activity = await _db.CandidateActivities
            .FirstOrDefaultAsync(a => a.Id == activityId && a.CandidateId == id);

        if (activity is null)
            return NotFound(ApiResponse<CandidateActivityResponseDto>.Fail("Activity not found."));

        return Ok(ApiResponse<CandidateActivityResponseDto>.Ok(MapToActivityResponse(activity), "Activity fetched successfully."));
    }

    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateActivityResponseDto>>> Reject(int id, [FromBody] RejectCandidateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(ApiResponse<CandidateActivityResponseDto>.Fail("Reason is required."));

        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateActivityResponseDto>.Fail("Candidate not found."));

        var now = DateTime.UtcNow;
        var activity = new CandidateActivity
        {
            CandidateId = id,
            ActivityType = "Rejected",
            Stage = "Rejected",
            Status = "Rejected",
            Remarks = dto.Reason,
            ActionDate = now,
            CreatedBy = dto.CreatedBy,
            CreatedAt = now
        };

        candidate.CurrentStage = "Rejected";
        candidate.CurrentStatus = "Rejected";
        candidate.LastActivityDate = now;
        candidate.Remarks = dto.Reason;
        candidate.UpdatedAt = now;

        _db.CandidateActivities.Add(activity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetActivityById), new { id = candidate.CandidateId, activityId = activity.Id },
            ApiResponse<CandidateActivityResponseDto>.Ok(MapToActivityResponse(activity), "Candidate rejected successfully."));
    }

    [HttpPost("{id:int}/offer")]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateActivityResponseDto>>> Offer(int id, [FromBody] OfferCandidateDto dto)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateActivityResponseDto>.Fail("Candidate not found."));

        var now = DateTime.UtcNow;
        var evaluationPayload = new
        {
            offeredCtc = dto.OfferedCtc,
            joiningDate = dto.JoiningDate?.ToString("o")
        };

        var activity = new CandidateActivity
        {
            CandidateId = id,
            ActivityType = "OfferReleased",
            Stage = "Offer",
            Status = "Offer Released",
            EvaluationJson = ConvertToJsonDocument(evaluationPayload),
            Remarks = dto.Remarks,
            ActionDate = now,
            CreatedBy = dto.CreatedBy,
            CreatedAt = now
        };

        candidate.CurrentStage = "Offer";
        candidate.CurrentStatus = "Offer Released";
        candidate.LastActivityDate = now;
        candidate.NextFollowUpDate = dto.JoiningDate;
        candidate.Remarks = dto.Remarks;
        candidate.UpdatedAt = now;

        _db.CandidateActivities.Add(activity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetActivityById), new { id = candidate.CandidateId, activityId = activity.Id },
            ApiResponse<CandidateActivityResponseDto>.Ok(MapToActivityResponse(activity), "Offer released successfully."));
    }

    [HttpPost("{id:int}/join")]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateActivityResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateActivityResponseDto>>> Join(int id, [FromBody] OfferCandidateDto dto)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateActivityResponseDto>.Fail("Candidate not found."));

        var now = DateTime.UtcNow;
        var evaluationPayload = new
        {
            offeredCtc = dto.OfferedCtc,
            joiningDate = dto.JoiningDate?.ToString("o")
        };

        var activity = new CandidateActivity
        {
            CandidateId = id,
            ActivityType = "Joined",
            Stage = "Joined",
            Status = "Joined",
            EvaluationJson = ConvertToJsonDocument(evaluationPayload),
            Remarks = dto.Remarks,
            ActionDate = now,
            CreatedBy = dto.CreatedBy,
            CreatedAt = now
        };

        candidate.CurrentStage = "Joined";
        candidate.CurrentStatus = "Joined";
        candidate.LastActivityDate = now;
        candidate.Remarks = dto.Remarks;
        candidate.UpdatedAt = now;

        _db.CandidateActivities.Add(activity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetActivityById), new { id = candidate.CandidateId, activityId = activity.Id },
            ApiResponse<CandidateActivityResponseDto>.Ok(MapToActivityResponse(activity), "Candidate joined successfully."));
    }

    [HttpPost("{id:int}/resume")]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateResponseDto>>> UploadResume(
        int id,
        [FromBody] CandidateResumeUploadDto dto)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateResponseDto>.Fail("Candidate not found."));

        if (!TryDecodeBase64File(dto.ResumeBase64, out var resumeBytes) || resumeBytes is null)
            return BadRequest(ApiResponse<CandidateResponseDto>.Fail("ResumeBase64 must be valid Base64."));

        DeleteResumeFile(candidate.ResumePath);

        candidate.ResumePath = await SaveResumeAsync(resumeBytes, dto.ResumeFileName);
        candidate.ResumeFileName = SafeFileName(dto.ResumeFileName);
        candidate.ResumeContentType = dto.ResumeContentType;
        candidate.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<CandidateResponseDto>.Ok(MapToResponse(candidate), "Resume uploaded successfully."));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CandidateResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CandidateResponseDto>>> Delete(int id)
    {
        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted);

        if (candidate is null)
            return NotFound(ApiResponse<CandidateResponseDto>.Fail("Candidate not found."));

        candidate.IsDeleted = true;
        candidate.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<CandidateResponseDto>.Ok(MapToResponse(candidate), "Candidate deleted successfully."));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<string> SaveResumeAsync(byte[] bytes, string? fileName)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads", "resumes");

        Directory.CreateDirectory(uploadsDir);

        var safeFile = SafeFileName(fileName) ?? "resume";
        var uniqueName = $"{Guid.NewGuid()}_{safeFile}";
        var filePath = Path.Combine(uploadsDir, uniqueName);

        await System.IO.File.WriteAllBytesAsync(filePath, bytes);

        return $"/uploads/resumes/{uniqueName}";
    }

    private void DeleteResumeFile(string? relativePath)
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

    private static JsonDocument? ConvertToJsonDocument(object? value)
    {
        if (value is null)
            return null;

        if (value is JsonDocument document)
            return document;

        if (value is JsonElement element)
            return JsonDocument.Parse(element.GetRawText());

        var json = JsonSerializer.Serialize(value);
        return JsonDocument.Parse(json);
    }

    private static CandidateActivityResponseDto MapToActivityResponse(CandidateActivity activity) => new()
    {
        Id = activity.Id,
        ActivityType = activity.ActivityType,
        Stage = activity.Stage,
        Status = activity.Status,
        TotalScore = activity.TotalScore,
        EvaluationJson = activity.EvaluationJson,
        Remarks = activity.Remarks,
        ActionDate = activity.ActionDate,
        CreatedBy = activity.CreatedBy
    };

    private async Task<Dictionary<int, decimal?>> GetLatestAiScoresAsync(IEnumerable<int> candidateIds)
    {
        var ids = candidateIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, decimal?>();

        var screenings = await _db.CandidateAIScreenings
            .Where(s => ids.Contains(s.CandidateId))
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.CandidateId, s.OverallScore, s.CreatedAt })
            .ToListAsync();

        return screenings
            .GroupBy(s => s.CandidateId)
            .ToDictionary(g => g.Key, g => g.First().OverallScore);
    }

    private async Task<decimal?> GetLatestAiScoreAsync(int candidateId)
    {
        return await _db.CandidateAIScreenings
            .Where(s => s.CandidateId == candidateId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.OverallScore)
            .FirstOrDefaultAsync();
    }

    private static CandidateResponseDto MapToResponse(Candidate candidate, decimal? aiOverallScore = null) => new()
    {
        CandidateId = candidate.CandidateId,
        RequisitionId = candidate.RequisitionId,
        FullName = candidate.FullName,
        Email = candidate.Email,
        PhoneNumber = candidate.PhoneNumber,
        YearsOfExperience = candidate.YearsOfExperience,
        NoticePeriod = candidate.NoticePeriod,
        CurrentCtcLpa = candidate.CurrentCtcLpa,
        ExpectedCtcLpa = candidate.ExpectedCtcLpa,
        KeySkills = candidate.KeySkills,
        Source = candidate.Source,
        DateOfBirth = candidate.DateOfBirth,
        ResumePath = candidate.ResumePath,
        ResumeFileName = candidate.ResumeFileName,
        ResumeContentType = candidate.ResumeContentType,
        AssignedHRId = candidate.AssignedHRId,
        NextFollowUpDate = candidate.NextFollowUpDate,
        LastActivityDate = candidate.LastActivityDate,
        Remarks = candidate.Remarks,
        CurrentStage = candidate.CurrentStage,
        CurrentStatus = candidate.CurrentStatus,
        ShortlistingDate = candidate.ShortlistingDate,
        AiOverallScore = aiOverallScore,
        CreatedAt = candidate.CreatedAt,
        UpdatedAt = candidate.UpdatedAt
    };

    private static bool IsShortlistingStatus(string status) =>
        status.Equals("shorting", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("shortlisted", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("shortlisting", StringComparison.OrdinalIgnoreCase);
}
