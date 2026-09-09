using System.IdentityModel.Tokens.Jwt;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Controllers;

[ApiController]
[Authorize]
[Route("api/onboarding-assessments")]
[Produces("application/json")]
public sealed class OnboardingAssessmentsController : ControllerBase
{
    private readonly IOnboardingAssessmentService _service;
    private readonly AppDbContext _db;
    public OnboardingAssessmentsController(IOnboardingAssessmentService service, AppDbContext db) { _service = service; _db = db; }

    [HttpPost("{candidateId:int}/start")]
    [ProducesResponseType(typeof(ApiResponse<OnboardingAssessmentResponseDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentResponseDto>>> Start(int candidateId, StartOnboardingAssessmentDto request, CancellationToken ct)
    {
        var result = await _service.StartAsync(candidateId, request.DurationDays, request.DailyWorkingHours, await UserIdAsync(ct), ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<OnboardingAssessmentResponseDto>.Ok(result, "Onboarding Stage 2 assessment is ready."));
    }

    [HttpGet("{candidateId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentResponseDto>>> Get(int candidateId, CancellationToken ct) =>
        Ok(ApiResponse<OnboardingAssessmentResponseDto>.Ok(await _service.GetAsync(candidateId, ct), "Onboarding assessment fetched successfully."));

    [HttpGet("{candidateId:int}/current-day")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<CurrentOnboardingDayDto>>> CurrentDay(int candidateId, CancellationToken ct) =>
        Ok(ApiResponse<CurrentOnboardingDayDto>.Ok(await _service.GetCurrentDayAsync(candidateId, ct), "Current onboarding day fetched successfully."));

    [HttpPost("{candidateId:int}/days/{day:int}/submit")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentResponseDto>>> Submit(int candidateId, int day, DailyOnboardingSubmissionDto request, CancellationToken ct) =>
        Ok(ApiResponse<OnboardingAssessmentResponseDto>.Ok(await _service.SubmitDayAsync(candidateId, day, request, await UserIdAsync(ct), ct), $"Day {day} submitted successfully."));

    [HttpPut("{candidateId:int}/days/{day:int}/tasks/{taskId}/save")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentResponseDto>>> SaveTask(
        int candidateId, int day, string taskId, SaveOnboardingTaskDto request, CancellationToken ct) =>
        Ok(ApiResponse<OnboardingAssessmentResponseDto>.Ok(
            await _service.SaveTaskAsync(candidateId, day, taskId, request, await UserIdAsync(ct), ct),
            $"Task {taskId} progress saved successfully."));

    [HttpPost("{candidateId:int}/tasks/{taskId}/files")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<OnboardingSubmissionFileDto>>> UploadTaskFile(
        int candidateId, string taskId, IFormFile file, CancellationToken ct) =>
        Ok(ApiResponse<OnboardingSubmissionFileDto>.Ok(
            await _service.UploadTaskFileAsync(candidateId, taskId, file, ct), "Task file uploaded successfully."));

    [HttpPost("{candidateId:int}/evaluate")]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentResponseDto>>> Evaluate(int candidateId, CancellationToken ct) =>
        Ok(ApiResponse<OnboardingAssessmentResponseDto>.Ok(await _service.EvaluateAsync(candidateId, await UserIdAsync(ct), ct), "Final onboarding evaluation completed."));

    [HttpPost("{candidateId:int}/decision")]
    public async Task<ActionResult<ApiResponse<OnboardingAssessmentResponseDto>>> Decide(int candidateId, OnboardingHrDecisionDto request, CancellationToken ct) =>
        Ok(ApiResponse<OnboardingAssessmentResponseDto>.Ok(await _service.DecideAsync(candidateId, request, await UserIdAsync(ct), ct), "HR decision recorded successfully."));

    private async Task<int?> UserIdAsync(CancellationToken ct)
    {
        var employeeId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(employeeId)) return null;
        return await _db.Users.AsNoTracking().Where(u => u.EmployeeId == employeeId).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
    }
}
