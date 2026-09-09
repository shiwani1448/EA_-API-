using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Route("api/onboarding-assessment-question-test-bank")]
[Produces("application/json")]
public class OnboardingAssessmentQuestionTestBankController : ControllerBase
{
    private readonly AppDbContext _db;

    public OnboardingAssessmentQuestionTestBankController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>>> Create(
        [FromBody] OnboardingAssessmentQuestionTestBankDto dto)
    {
        var question = new OnboardingAssessmentQuestionTestBank
        {
            Question = dto.Question,
            Option1 = dto.Option1,
            Option2 = dto.Option2,
            Option3 = dto.Option3,
            Option4 = dto.Option4,
            CorrectAnswer = dto.CorrectAnswer,
            Category = dto.Category,
            IsActive = dto.IsActive,
            CreatedBy = dto.CreatedBy,
            CreationDate = DateTime.UtcNow
        };

        _db.OnboardingAssessmentQuestionTestBanks.Add(question);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { questionId = question.QuestionId },
            ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>.Ok(
                MapToResponse(question),
                "Onboarding assessment question created successfully."));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<OnboardingAssessmentQuestionTestBankResponseDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<OnboardingAssessmentQuestionTestBankResponseDto>>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] bool? isActive)
    {
        var query = _db.OnboardingAssessmentQuestionTestBanks.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(q => q.Category != null && q.Category.ToLower() == category.Trim().ToLower());

        if (isActive.HasValue)
            query = query.Where(q => q.IsActive == isActive.Value);

        var questions = await query
            .OrderByDescending(q => q.CreationDate)
            .ToListAsync();

        return Ok(ApiResponse<List<OnboardingAssessmentQuestionTestBankResponseDto>>.Ok(
            questions.Select(MapToResponse).ToList(),
            "Onboarding assessment questions fetched successfully."));
    }

    [HttpGet("{questionId:int}")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>>> GetById(int questionId)
    {
        var question = await _db.OnboardingAssessmentQuestionTestBanks
            .FirstOrDefaultAsync(q => q.QuestionId == questionId);

        if (question is null)
            return NotFound(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>.Fail("Onboarding assessment question not found."));

        return Ok(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>.Ok(
            MapToResponse(question),
            "Onboarding assessment question fetched successfully."));
    }

    [HttpPut("{questionId:int}")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>>> Update(
        int questionId,
        [FromBody] OnboardingAssessmentQuestionTestBankDto dto)
    {
        var question = await _db.OnboardingAssessmentQuestionTestBanks
            .FirstOrDefaultAsync(q => q.QuestionId == questionId);

        if (question is null)
            return NotFound(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>.Fail("Onboarding assessment question not found."));

        question.Question = dto.Question;
        question.Option1 = dto.Option1;
        question.Option2 = dto.Option2;
        question.Option3 = dto.Option3;
        question.Option4 = dto.Option4;
        question.CorrectAnswer = dto.CorrectAnswer;
        question.Category = dto.Category;
        question.IsActive = dto.IsActive;
        question.CreatedBy = dto.CreatedBy;
        question.UpdationDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>.Ok(
            MapToResponse(question),
            "Onboarding assessment question updated successfully."));
    }

    [HttpDelete("{questionId:int}")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>>> Delete(int questionId)
    {
        var question = await _db.OnboardingAssessmentQuestionTestBanks
            .FirstOrDefaultAsync(q => q.QuestionId == questionId);

        if (question is null)
            return NotFound(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>.Fail("Onboarding assessment question not found."));

        question.IsActive = false;
        question.UpdationDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<OnboardingAssessmentQuestionTestBankResponseDto>.Ok(
            MapToResponse(question),
            "Onboarding assessment question deactivated successfully."));
    }

    private static OnboardingAssessmentQuestionTestBankResponseDto MapToResponse(
        OnboardingAssessmentQuestionTestBank question) => new()
    {
        QuestionId = question.QuestionId,
        Question = question.Question,
        Option1 = question.Option1,
        Option2 = question.Option2,
        Option3 = question.Option3,
        Option4 = question.Option4,
        CorrectAnswer = question.CorrectAnswer,
        Category = question.Category,
        IsActive = question.IsActive,
        CreatedBy = question.CreatedBy,
        CreationDate = question.CreationDate,
        UpdationDate = question.UpdationDate
    };
}
