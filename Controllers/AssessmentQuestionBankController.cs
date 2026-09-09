using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AssessmentQuestionBankController : ControllerBase
{
    private static readonly HashSet<string> AllowedQuestionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Technical",
        "Practical",
        "Scenario",
        "Case Study",
        "Behavioral"
    };

    private static readonly HashSet<string> AllowedDifficulties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Easy",
        "Medium",
        "Hard"
    };

    private readonly AppDbContext _db;

    public AssessmentQuestionBankController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AssessmentQuestionBankResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AssessmentQuestionBankResponseDto>>>> GetAll()
    {
        var questions = await _db.AssessmentQuestionBanks
            .OrderByDescending(q => q.CreatedAt)
            .ThenBy(q => q.QuestionNo)
            .ToListAsync();

        return Ok(ApiResponse<List<AssessmentQuestionBankResponseDto>>.Ok(
            questions.Select(MapToResponse).ToList(),
            "Assessment question bank fetched successfully."));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AssessmentQuestionBankResponseDto>>> GetById(int id)
    {
        var question = await _db.AssessmentQuestionBanks.FirstOrDefaultAsync(q => q.Id == id);

        if (question is null)
            return NotFound(ApiResponse<AssessmentQuestionBankResponseDto>.Fail("Assessment question not found."));

        return Ok(ApiResponse<AssessmentQuestionBankResponseDto>.Ok(
            MapToResponse(question),
            "Assessment question fetched successfully."));
    }

    [HttpGet("by-filter")]
    [ProducesResponseType(typeof(ApiResponse<List<AssessmentQuestionBankResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<AssessmentQuestionBankResponseDto>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<AssessmentQuestionBankResponseDto>>>> GetByFilter(
        [FromQuery] AssessmentQuestionBankFilterDto filter)
    {
        var validationErrors = ValidateFilter(filter);
        if (validationErrors.Count > 0)
            return BadRequest(ApiResponse<List<AssessmentQuestionBankResponseDto>>.Fail(
                "Invalid filter.",
                validationErrors));

        var department = NormalizeRequired(filter.Department);
        var designation = NormalizeRequired(filter.Designation);
        var level = NormalizeRequired(filter.Level);

        var questions = await _db.AssessmentQuestionBanks
            .Where(q => q.IsActive)
            .Where(q => q.Department.ToLower() == department.ToLower()
                && q.Designation.ToLower() == designation.ToLower()
                && q.Level.ToLower() == level.ToLower())
            .OrderBy(q => q.RoundName)
            .ThenBy(q => q.QuestionNo)
            .ToListAsync();

        return Ok(ApiResponse<List<AssessmentQuestionBankResponseDto>>.Ok(
            questions.Select(MapToResponse).ToList(),
            "Assessment questions fetched successfully."));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AssessmentQuestionBankResponseDto>>> Create(
        [FromBody] AssessmentQuestionBankDto dto)
    {
        var validationErrors = ValidateQuestion(dto);
        if (validationErrors.Count > 0)
            return BadRequest(ApiResponse<AssessmentQuestionBankResponseDto>.Fail(
                "Invalid assessment question.",
                validationErrors));

        if (await IsDuplicateAsync(dto))
            return BadRequest(ApiResponse<AssessmentQuestionBankResponseDto>.Fail(
                "QuestionNo already exists for this Department, Designation, Level, and RoundName."));

        var question = MapToEntity(dto);
        question.CreatedAt = DateTime.UtcNow;

        _db.AssessmentQuestionBanks.Add(question);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = question.Id },
            ApiResponse<AssessmentQuestionBankResponseDto>.Ok(
                MapToResponse(question),
                "Assessment question created successfully."));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AssessmentQuestionBankResponseDto>>> Update(
        int id,
        [FromBody] AssessmentQuestionBankDto dto)
    {
        var question = await _db.AssessmentQuestionBanks.FirstOrDefaultAsync(q => q.Id == id);

        if (question is null)
            return NotFound(ApiResponse<AssessmentQuestionBankResponseDto>.Fail("Assessment question not found."));

        var validationErrors = ValidateQuestion(dto);
        if (validationErrors.Count > 0)
            return BadRequest(ApiResponse<AssessmentQuestionBankResponseDto>.Fail(
                "Invalid assessment question.",
                validationErrors));

        if (await IsDuplicateAsync(dto, id))
            return BadRequest(ApiResponse<AssessmentQuestionBankResponseDto>.Fail(
                "QuestionNo already exists for this Department, Designation, Level, and RoundName."));

        ApplyUpdate(question, dto);
        question.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<AssessmentQuestionBankResponseDto>.Ok(
            MapToResponse(question),
            "Assessment question updated successfully."));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AssessmentQuestionBankResponseDto>>> Delete(int id)
    {
        var question = await _db.AssessmentQuestionBanks.FirstOrDefaultAsync(q => q.Id == id);

        if (question is null)
            return NotFound(ApiResponse<AssessmentQuestionBankResponseDto>.Fail("Assessment question not found."));

        question.IsActive = false;
        question.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<AssessmentQuestionBankResponseDto>.Ok(
            MapToResponse(question),
            "Assessment question deactivated successfully."));
    }

    [HttpPatch("{id:int}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AssessmentQuestionBankResponseDto>), StatusCodes.Status404NotFound)]
    public Task<ActionResult<ApiResponse<AssessmentQuestionBankResponseDto>>> Deactivate(int id) => Delete(id);

    [HttpPost("bulk-upload")]
    [ProducesResponseType(typeof(ApiResponse<List<AssessmentQuestionBankResponseDto>>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<List<AssessmentQuestionBankResponseDto>>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<AssessmentQuestionBankResponseDto>>>> BulkUpload(
        [FromBody] AssessmentQuestionBankBulkUploadDto dto)
    {
        var validationErrors = ValidateBulkUpload(dto);
        if (validationErrors.Count > 0)
            return BadRequest(ApiResponse<List<AssessmentQuestionBankResponseDto>>.Fail(
                "Invalid assessment question bulk upload.",
                validationErrors));

        var department = NormalizeRequired(dto.Department);
        var designation = NormalizeRequired(dto.Designation);
        var level = NormalizeRequired(dto.Level);
        var roundName = NormalizeRequired(dto.RoundName);
        var questionNos = dto.Questions.Select(q => q.QuestionNo).ToList();

        var existingQuestionNos = await _db.AssessmentQuestionBanks
            .Where(q => q.Department.ToLower() == department.ToLower()
                && q.Designation.ToLower() == designation.ToLower()
                && q.Level.ToLower() == level.ToLower()
                && q.RoundName.ToLower() == roundName.ToLower()
                && questionNos.Contains(q.QuestionNo))
            .Select(q => q.QuestionNo)
            .ToListAsync();

        if (existingQuestionNos.Count > 0)
            return BadRequest(ApiResponse<List<AssessmentQuestionBankResponseDto>>.Fail(
                "Duplicate QuestionNo found for this Department, Designation, Level, and RoundName.",
                existingQuestionNos.OrderBy(n => n).Select(n => $"QuestionNo {n} already exists.").ToList()));

        var now = DateTime.UtcNow;
        var questions = dto.Questions.Select(q => new AssessmentQuestionBank
        {
            Department = department,
            Designation = designation,
            Level = level,
            RoundName = roundName,
            QuestionNo = q.QuestionNo,
            QuestionType = NormalizeRequired(q.QuestionType),
            SkillArea = NormalizeOptional(q.SkillArea),
            Difficulty = NormalizeRequired(q.Difficulty),
            Question = NormalizeRequired(q.Question),
            ExpectedAnswer = NormalizeOptional(q.ExpectedAnswer),
            MaxScore = q.MaxScore,
            Weightage = q.Weightage,
            IsActive = q.IsActive,
            CreatedAt = now
        }).ToList();

        _db.AssessmentQuestionBanks.AddRange(questions);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll),
            ApiResponse<List<AssessmentQuestionBankResponseDto>>.Ok(
                questions.Select(MapToResponse).ToList(),
                "Assessment questions uploaded successfully."));
    }

    private static List<string> ValidateFilter(AssessmentQuestionBankFilterDto dto)
    {
        var errors = new List<string>();
        AddRequired(errors, dto.Department, "Department");
        AddRequired(errors, dto.Designation, "Designation");
        AddRequired(errors, dto.Level, "Level");
        return errors;
    }

    private static List<string> ValidateQuestion(AssessmentQuestionBankDto dto)
    {
        var errors = new List<string>();
        AddRequired(errors, dto.Department, "Department");
        AddRequired(errors, dto.Designation, "Designation");
        AddRequired(errors, dto.Level, "Level");
        AddRequired(errors, dto.RoundName, "RoundName");
        AddRequired(errors, dto.QuestionType, "QuestionType");
        AddRequired(errors, dto.Difficulty, "Difficulty");
        AddRequired(errors, dto.Question, "Question");

        if (dto.QuestionNo <= 0)
            errors.Add("QuestionNo must be greater than 0.");

        if (dto.MaxScore <= 0)
            errors.Add("MaxScore must be greater than 0.");

        if (dto.Weightage <= 0)
            errors.Add("Weightage must be greater than 0.");

        if (!string.IsNullOrWhiteSpace(dto.QuestionType) && !AllowedQuestionTypes.Contains(dto.QuestionType.Trim()))
            errors.Add("QuestionType must be Technical, Practical, Scenario, Case Study, or Behavioral.");

        if (!string.IsNullOrWhiteSpace(dto.Difficulty) && !AllowedDifficulties.Contains(dto.Difficulty.Trim()))
            errors.Add("Difficulty must be Easy, Medium, or Hard.");

        return errors;
    }

    private static List<string> ValidateBulkUpload(AssessmentQuestionBankBulkUploadDto dto)
    {
        var errors = ValidateFilter(new AssessmentQuestionBankFilterDto
        {
            Department = dto.Department,
            Designation = dto.Designation,
            Level = dto.Level,
            RoundName = dto.RoundName
        });

        if (dto.Questions.Count == 0)
            errors.Add("Questions are required.");

        var duplicateQuestionNos = dto.Questions
            .GroupBy(q => q.QuestionNo)
            .Where(g => g.Key > 0 && g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var questionNo in duplicateQuestionNos)
            errors.Add($"QuestionNo {questionNo} is duplicated in upload payload.");

        for (var i = 0; i < dto.Questions.Count; i++)
        {
            var q = dto.Questions[i];
            var item = new AssessmentQuestionBankDto
            {
                Department = dto.Department,
                Designation = dto.Designation,
                Level = dto.Level,
                RoundName = dto.RoundName,
                QuestionNo = q.QuestionNo,
                QuestionType = q.QuestionType,
                SkillArea = q.SkillArea,
                Difficulty = q.Difficulty,
                Question = q.Question,
                ExpectedAnswer = q.ExpectedAnswer,
                MaxScore = q.MaxScore,
                Weightage = q.Weightage,
                IsActive = q.IsActive
            };

            foreach (var error in ValidateQuestion(item))
                errors.Add($"Questions[{i}]: {error}");
        }

        return errors;
    }

    private async Task<bool> IsDuplicateAsync(AssessmentQuestionBankDto dto, int? excludeId = null)
    {
        var department = NormalizeRequired(dto.Department);
        var designation = NormalizeRequired(dto.Designation);
        var level = NormalizeRequired(dto.Level);
        var roundName = NormalizeRequired(dto.RoundName);

        return await _db.AssessmentQuestionBanks
            .Where(q => !excludeId.HasValue || q.Id != excludeId.Value)
            .AnyAsync(q => q.Department.ToLower() == department.ToLower()
                && q.Designation.ToLower() == designation.ToLower()
                && q.Level.ToLower() == level.ToLower()
                && q.RoundName.ToLower() == roundName.ToLower()
                && q.QuestionNo == dto.QuestionNo);
    }

    private static AssessmentQuestionBank MapToEntity(AssessmentQuestionBankDto dto) => new()
    {
        Department = NormalizeRequired(dto.Department),
        Designation = NormalizeRequired(dto.Designation),
        Level = NormalizeRequired(dto.Level),
        RoundName = NormalizeRequired(dto.RoundName),
        QuestionNo = dto.QuestionNo,
        QuestionType = NormalizeRequired(dto.QuestionType),
        SkillArea = NormalizeOptional(dto.SkillArea),
        Difficulty = NormalizeRequired(dto.Difficulty),
        Question = NormalizeRequired(dto.Question),
        ExpectedAnswer = NormalizeOptional(dto.ExpectedAnswer),
        MaxScore = dto.MaxScore,
        Weightage = dto.Weightage,
        IsActive = dto.IsActive
    };

    private static void ApplyUpdate(AssessmentQuestionBank question, AssessmentQuestionBankDto dto)
    {
        question.Department = NormalizeRequired(dto.Department);
        question.Designation = NormalizeRequired(dto.Designation);
        question.Level = NormalizeRequired(dto.Level);
        question.RoundName = NormalizeRequired(dto.RoundName);
        question.QuestionNo = dto.QuestionNo;
        question.QuestionType = NormalizeRequired(dto.QuestionType);
        question.SkillArea = NormalizeOptional(dto.SkillArea);
        question.Difficulty = NormalizeRequired(dto.Difficulty);
        question.Question = NormalizeRequired(dto.Question);
        question.ExpectedAnswer = NormalizeOptional(dto.ExpectedAnswer);
        question.MaxScore = dto.MaxScore;
        question.Weightage = dto.Weightage;
        question.IsActive = dto.IsActive;
    }

    private static AssessmentQuestionBankResponseDto MapToResponse(AssessmentQuestionBank question) => new()
    {
        Id = question.Id,
        Department = question.Department,
        Designation = question.Designation,
        Level = question.Level,
        RoundName = question.RoundName,
        QuestionNo = question.QuestionNo,
        QuestionType = question.QuestionType,
        SkillArea = question.SkillArea,
        Difficulty = question.Difficulty,
        Question = question.Question,
        ExpectedAnswer = question.ExpectedAnswer,
        MaxScore = question.MaxScore,
        Weightage = question.Weightage,
        IsActive = question.IsActive,
        CreatedAt = question.CreatedAt,
        UpdatedAt = question.UpdatedAt
    };

    private static void AddRequired(List<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{fieldName} is required.");
    }

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
