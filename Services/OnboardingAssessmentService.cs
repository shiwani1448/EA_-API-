using System.Text.Json;
using System.Text.Json.Nodes;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Services;

public sealed class OnboardingAssessmentService : IOnboardingAssessmentService
{
    private readonly AppDbContext _db;
    private readonly IOnboardingAiService _ai;
    private readonly IOnboardingFileService _files;
    private readonly IOnboardingEvidenceProcessor _evidence;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ActiveStatuses = ["InProgress", "ReadyForFinalEvaluation", "Evaluated", "Held"];

    public OnboardingAssessmentService(AppDbContext db, IOnboardingAiService ai, IOnboardingFileService files, IOnboardingEvidenceProcessor evidence)
    { _db = db; _ai = ai; _files = files; _evidence = evidence; }

    public async Task<OnboardingAssessmentResponseDto> StartAsync(int candidateId, int durationDays, decimal dailyWorkingHours, int? userId, CancellationToken ct)
    {
        if (durationDays is < 1 or > 30) throw new ArgumentException("DurationDays must be between 1 and 30.");
        if (dailyWorkingHours is < 1 or > 12) throw new ArgumentException("DailyWorkingHours must be between 1 and 12.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LockCandidateAsync(candidateId, ct);
        var candidate = await CandidateAsync(candidateId, ct);
        var existing = await _db.OnboardingAssessments.Where(a => a.CandidateId == candidateId && ActiveStatuses.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);
        if (existing is not null) return Map(existing);
        if (!Equal(candidate.CurrentStage, "Onboarding") || !Equal(candidate.CurrentStatus, "Stage 1 Completed"))
            throw new InvalidOperationException("Candidate must be at Onboarding / Stage 1 Completed to start Stage 2.");

        var profile = await BuildAiInputAsync(candidate, durationDays, dailyWorkingHours, ct);
        var plan = await _ai.GeneratePlanAsync(profile, durationDays, ct);
        var now = DateTime.UtcNow;
        var assessment = new OnboardingAssessment
        {
            CandidateId = candidateId, DurationDays = durationDays, StartDate = now,
            ExpectedEndDate = now.AddDays(durationDays), CurrentDay = 1, Status = "InProgress",
            PlanJson = plan, ResponsesJson = JsonDocument.Parse("{\"days\":[]}"), CreatedAt = now, CreatedBy = userId
        };
        _db.OnboardingAssessments.Add(assessment);
        await _db.SaveChangesAsync(ct);
        candidate.CurrentStage = "Onboarding"; candidate.CurrentStatus = "Stage 2 In Progress";
        candidate.LastActivityDate = now; candidate.UpdatedAt = now;
        AddActivity(candidateId, "OnboardingAssessmentStarted", "Onboarding", "Stage 2 In Progress", userId, now,
            new { assessmentId = assessment.Id, durationDays, startedAt = now });
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Map(assessment);
    }

    public async Task<OnboardingAssessmentResponseDto> GetAsync(int candidateId, CancellationToken ct)
    {
        await CandidateAsync(candidateId, ct);
        return Map(await LatestAsync(candidateId, ct));
    }

    public async Task<CurrentOnboardingDayDto> GetCurrentDayAsync(int candidateId, CancellationToken ct)
    {
        await CandidateAsync(candidateId, ct);
        var assessment = await LatestAsync(candidateId, ct);
        if (assessment.Status != "InProgress") throw new InvalidOperationException("Assessment has no pending current day.");
        var day = FindDay(assessment.PlanJson.RootElement, assessment.CurrentDay);
        var savedTasks = GetSavedTasks(assessment.ResponsesJson.RootElement, assessment.CurrentDay);
        return new CurrentOnboardingDayDto
        {
            AssessmentId = assessment.Id, Day = assessment.CurrentDay,
            Title = StringOrNull(day, "title"), Objective = StringOrNull(day, "objective"),
            Tasks = ArrayOrEmpty(day, "tasks"), SavedTasks = savedTasks,
            SubmissionStatus = savedTasks.GetArrayLength() > 0 ? "IN_PROGRESS" : "NOT_STARTED"
        };
    }

    public async Task<OnboardingAssessmentResponseDto> SubmitDayAsync(int candidateId, int dayNumber, DailyOnboardingSubmissionDto request, int? userId, CancellationToken ct)
    {
        request.Tasks ??= new();
        await using var tx = await _db.Database.BeginTransactionAsync(ct); await LockCandidateAsync(candidateId, ct);
        var candidate = await CandidateAsync(candidateId, ct); var assessment = await LatestAsync(candidateId, ct);
        if (assessment.Status != "InProgress") throw new InvalidOperationException("Assessment is not accepting daily submissions.");
        if (dayNumber < 1 || dayNumber > assessment.DurationDays) throw new ArgumentException("Day is outside the assessment duration.");
        if (dayNumber != assessment.CurrentDay) throw new InvalidOperationException($"Day {assessment.CurrentDay} must be submitted next.");
        var planDay = FindDay(assessment.PlanJson.RootElement, dayNumber);
        foreach (var task in request.Tasks)
            foreach (var file in task.Files ?? [])
                if (!_files.IsValidReference(candidateId, assessment.Id, task.TaskId, file))
                    throw new ArgumentException($"Task {task.TaskId} contains an invalid or missing file reference.");
        var responses = JsonNode.Parse(assessment.ResponsesJson.RootElement.GetRawText())!.AsObject();
        var days = responses["days"]!.AsArray();
        var now = DateTime.UtcNow;
        var dayNode = GetOrCreateResponseDay(days, dayNumber);
        if (dayNode["status"]?.GetValue<string>() == "SUBMITTED") throw new InvalidOperationException("This day has already been submitted.");
        foreach (var task in request.Tasks) UpsertTask(dayNode, task.TaskId, "SUBMITTED", task.TextResponse, task.Files, now);
        var combined = new DailyOnboardingSubmissionDto
        {
            Tasks = dayNode["tasks"]!.AsArray().Deserialize<List<OnboardingTaskSubmissionDto>>(JsonOptions) ?? []
        };
        ValidateSubmission(planDay, combined);
        dayNode["status"] = "SUBMITTED"; dayNode["submittedAt"] = now;
        assessment.ResponsesJson = JsonDocument.Parse(responses.ToJsonString()); assessment.UpdatedAt = now; assessment.UpdatedBy = userId;
        var finalDay = dayNumber == assessment.DurationDays;
        assessment.Status = finalDay ? "ReadyForFinalEvaluation" : "InProgress";
        if (!finalDay) assessment.CurrentDay++;
        candidate.CurrentStage = "Onboarding"; candidate.CurrentStatus = finalDay ? "Assessment Submitted" : "Stage 2 In Progress";
        candidate.LastActivityDate = now; candidate.UpdatedAt = now;
        AddActivity(candidateId, "OnboardingDayCompleted", "Onboarding", $"Day {dayNumber} Completed", userId, now,
            new { assessmentId = assessment.Id, day = dayNumber, submittedAt = now, taskCount = request.Tasks.Count });
        if (finalDay) AddActivity(candidateId, "OnboardingAssessmentSubmitted", "Onboarding", "Assessment Submitted", userId, now,
            new { assessmentId = assessment.Id, submittedAt = now });
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(assessment);
    }

    public async Task<OnboardingAssessmentResponseDto> SaveTaskAsync(int candidateId, int dayNumber, string taskId, SaveOnboardingTaskDto request, int? userId, CancellationToken ct)
    {
        request.Files ??= new();
        var status = request.Status.Trim().ToUpperInvariant();
        if (status is not ("IN_PROGRESS" or "SUBMITTED")) throw new ArgumentException("Task status must be IN_PROGRESS or SUBMITTED.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct); await LockCandidateAsync(candidateId, ct);
        await CandidateAsync(candidateId, ct); var assessment = await LatestAsync(candidateId, ct);
        if (assessment.Status != "InProgress") throw new InvalidOperationException("Assessment is not accepting task progress.");
        if (dayNumber != assessment.CurrentDay) throw new InvalidOperationException($"Day {assessment.CurrentDay} is the current day.");
        var planDay = FindDay(assessment.PlanJson.RootElement, dayNumber);
        var plannedTask = ArrayOrEmpty(planDay, "tasks").EnumerateArray().FirstOrDefault(t =>
            t.GetProperty("taskId").GetString()?.Equals(taskId, StringComparison.OrdinalIgnoreCase) == true);
        if (plannedTask.ValueKind == JsonValueKind.Undefined) throw new KeyNotFoundException("Task was not found in the current day plan.");
        foreach (var file in request.Files)
            if (!_files.IsValidReference(candidateId, assessment.Id, taskId, file)) throw new ArgumentException("Task contains an invalid or missing file reference.");
        if (status == "SUBMITTED") ValidateTaskSubmission(plannedTask, new OnboardingTaskSubmissionDto
            { TaskId = taskId, Status = status, TextResponse = request.TextResponse, Files = request.Files });
        var responses = JsonNode.Parse(assessment.ResponsesJson.RootElement.GetRawText())!.AsObject();
        var dayNode = GetOrCreateResponseDay(responses["days"]!.AsArray(), dayNumber);
        if (dayNode["status"]?.GetValue<string>() == "SUBMITTED") throw new InvalidOperationException("The day has already been submitted.");
        var now = DateTime.UtcNow; UpsertTask(dayNode, taskId, status, request.TextResponse, request.Files, now);
        dayNode["status"] = "IN_PROGRESS"; dayNode["updatedAt"] = now;
        assessment.ResponsesJson = JsonDocument.Parse(responses.ToJsonString()); assessment.UpdatedAt = now; assessment.UpdatedBy = userId;
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(assessment);
    }

    public async Task<OnboardingAssessmentResponseDto> EvaluateAsync(int candidateId, int? userId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct); await LockCandidateAsync(candidateId, ct);
        var candidate = await CandidateAsync(candidateId, ct); var assessment = await LatestAsync(candidateId, ct);
        if (assessment.Status is "Evaluated" or "Held" or "Completed") return Map(assessment);
        if (assessment.Status != "ReadyForFinalEvaluation") throw new InvalidOperationException("All days must be submitted before final evaluation.");
        var submitted = assessment.ResponsesJson.RootElement.GetProperty("days").GetArrayLength();
        if (submitted != assessment.DurationDays) throw new InvalidOperationException("All days must be submitted before final evaluation.");
        ValidateAllRequiredTasksCompleted(assessment);
        var dailyHours = assessment.PlanJson.RootElement.TryGetProperty("dailyTargetHours", out var h) && h.TryGetDecimal(out var parsed) ? parsed : 8;
        var profile = await BuildAiInputAsync(candidate, assessment.DurationDays, dailyHours, ct);
        var processedEvidence = await _evidence.ProcessAsync(candidateId, assessment.Id, assessment.ResponsesJson, ct);
        var input = new { candidateAndRole = profile, plan = assessment.PlanJson.RootElement, responses = assessment.ResponsesJson.RootElement, processedEvidence };
        var aiEvaluation = await _ai.EvaluateFinalAsync(input, assessment.DurationDays, ct);
        var evaluationNode = JsonNode.Parse(aiEvaluation.RootElement.GetRawText())!.AsObject();
        evaluationNode["evidenceSummary"] = JsonSerializer.SerializeToNode(new
        {
            tasksEvaluated = processedEvidence.Tasks.Count,
            filesReviewed = processedEvidence.FilesReviewed,
            manualReviewRequired = processedEvidence.ManualReviewRequired
        }, JsonOptions);
        var evaluation = JsonDocument.Parse(evaluationNode.ToJsonString());
        var now = DateTime.UtcNow; assessment.FinalEvaluationJson = evaluation; assessment.Status = "Evaluated";
        assessment.CompletedAt = now; assessment.UpdatedAt = now; assessment.UpdatedBy = userId;
        candidate.CurrentStage = "Onboarding"; candidate.CurrentStatus = "Assessment Review"; candidate.UpdatedAt = now;
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(assessment);
    }

    public async Task<OnboardingSubmissionFileDto> UploadTaskFileAsync(int candidateId, string taskId, IFormFile file, CancellationToken ct)
    {
        await CandidateAsync(candidateId, ct); var assessment = await LatestAsync(candidateId, ct);
        if (assessment.Status != "InProgress") throw new InvalidOperationException("Assessment is not accepting uploads.");
        var currentDay = FindDay(assessment.PlanJson.RootElement, assessment.CurrentDay);
        var exists = ArrayOrEmpty(currentDay, "tasks").EnumerateArray().Any(t =>
            t.TryGetProperty("taskId", out var id) && id.GetString()?.Equals(taskId, StringComparison.OrdinalIgnoreCase) == true);
        if (!exists) throw new KeyNotFoundException("Task was not found in the current day plan.");
        return await _files.SaveAsync(candidateId, assessment.Id, taskId, file, ct);
    }

    public async Task<OnboardingAssessmentResponseDto> DecideAsync(int candidateId, OnboardingHrDecisionDto request, int? userId, CancellationToken ct)
    {
        var decision = request.Decision.Trim().ToUpperInvariant();
        if (decision is not ("APPROVE" or "HOLD" or "REJECT")) throw new ArgumentException("Decision must be APPROVE, HOLD, or REJECT.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct); await LockCandidateAsync(candidateId, ct);
        var candidate = await CandidateAsync(candidateId, ct); var assessment = await LatestAsync(candidateId, ct);
        if (assessment.Status == "Completed" && decision == "APPROVE") return Map(assessment);
        if (assessment.Status == "Rejected" && decision == "REJECT") return Map(assessment);
        if (assessment.Status == "Held" && decision == "HOLD") return Map(assessment);
        if (assessment.Status is not ("Evaluated" or "Held") || assessment.FinalEvaluationJson is null)
            throw new InvalidOperationException("The assessment must be AI-evaluated before an HR decision.");
        var now = DateTime.UtcNow; var aiRoot = assessment.FinalEvaluationJson.RootElement;
        var aiResult = aiRoot.GetProperty("result").GetString(); var aiScore = aiRoot.GetProperty("overallScore").GetDouble();
        string activityType, status;
        if (decision == "APPROVE")
        {
            candidate.CurrentStage = "Joined"; candidate.CurrentStatus = "Completed";
            assessment.Status = "Completed"; assessment.CompletedAt = now; activityType = "OnboardingCompleted"; status = "Completed";
        }
        else if (decision == "HOLD")
        {
            candidate.CurrentStage = "Onboarding"; candidate.CurrentStatus = "Assessment Review";
            assessment.Status = "Held"; activityType = "OnboardingAssessmentReviewed"; status = "Hold";
        }
        else
        {
            candidate.CurrentStage = "Rejected"; candidate.CurrentStatus = "Rejected";
            assessment.Status = "Rejected"; assessment.CompletedAt = now; activityType = "OnboardingAssessmentReviewed"; status = "Rejected";
        }
        assessment.UpdatedAt = now; assessment.UpdatedBy = userId; candidate.UpdatedAt = now; candidate.LastActivityDate = now; candidate.Remarks = request.Remarks;
        AddActivity(candidateId, activityType, decision == "APPROVE" ? "Onboarding" : candidate.CurrentStage, status, userId, now,
            new { assessmentId = assessment.Id, aiResult, aiScore, hrDecision = decision, hrRemarks = request.Remarks, completedAt = now }, request.Remarks);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Map(assessment);
    }

    private async Task<object> BuildAiInputAsync(Candidate candidate, int durationDays, decimal dailyWorkingHours, CancellationToken ct)
    {
        var hiring = await _db.HiringRequests.AsNoTracking().FirstOrDefaultAsync(h => h.RequestId == candidate.RequisitionId && !h.IsDeleted, ct);
        var jd = hiring?.JDID is int jdId ? await _db.JDMasters.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jdId && !j.IsDeleted, ct) : null;
        var department = hiring?.Department ?? jd?.Department;
        var designation = hiring?.Designation ?? jd?.Designation;
        if (string.IsNullOrWhiteSpace(designation))
            throw new InvalidOperationException("Candidate requisition does not contain a designation; a role-specific assessment cannot be generated.");

        return new
        {
            candidateId = candidate.CandidateId, candidate.FullName, department,
            designation, experienceYears = candidate.YearsOfExperience,
            salaryCtcLpa = candidate.CurrentCtcLpa ?? candidate.ExpectedCtcLpa, onboardingDurationDays = durationDays, dailyWorkingHours,
            jobDescription = jd?.JobDescription ?? hiring?.ReasonForHiring, requiredSkills = jd?.SkillsRequired ?? candidate.KeySkills,
            responsibilities = jd?.JobDescription, qualifications = jd?.Qualification, requiredExperience = jd?.Experience ?? hiring?.ExperienceRequired,
            excludedContent = "All questions, quizzes, HR policy, attendance, leave, working hours, code of conduct, payroll and generic administration"
        };
    }

    private async Task<Candidate> CandidateAsync(int id, CancellationToken ct) =>
        await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == id && !c.IsDeleted, ct) ?? throw new KeyNotFoundException("Candidate not found.");
    private async Task<OnboardingAssessment> LatestAsync(int id, CancellationToken ct) =>
        await _db.OnboardingAssessments.Where(a => a.CandidateId == id).OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Onboarding assessment not found.");
    private Task LockCandidateAsync(int id, CancellationToken ct) => _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(72002, {id})", ct);

    private void AddActivity(int candidateId, string type, string stage, string status, int? userId, DateTime now, object metadata, string? remarks = null) =>
        _db.CandidateActivities.Add(new CandidateActivity { CandidateId = candidateId, ActivityType = type, Stage = stage, Status = status,
            EvaluationJson = JsonDocument.Parse(JsonSerializer.Serialize(metadata, JsonOptions)), Remarks = remarks, ActionDate = now, CreatedAt = now, CreatedBy = userId });

    private static void ValidateSubmission(JsonElement day, DailyOnboardingSubmissionDto request)
    {
        var taskMap = request.Tasks.GroupBy(t => t.TaskId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        if (taskMap.Any(x => x.Value.Count > 1)) throw new ArgumentException("Duplicate task IDs are not allowed.");
        var tasks = ArrayOrEmpty(day, "tasks").EnumerateArray().ToList();
        var taskIds = tasks.Select(t => t.GetProperty("taskId").GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (taskMap.Keys.Any(id => !taskIds.Contains(id))) throw new ArgumentException("Submission contains a task that is not in this day's plan.");
        foreach (var planned in tasks.Where(Required))
        {
            var id = planned.GetProperty("taskId").GetString()!;
            if (!taskMap.TryGetValue(id, out var matches)) throw new ArgumentException($"Required task {id} must be submitted.");
            ValidateTaskSubmission(planned, matches[0]);
        }
    }
    private static void ValidateTaskSubmission(JsonElement planned, OnboardingTaskSubmissionDto submission)
    {
        var id = planned.GetProperty("taskId").GetString()!; submission.Files ??= new();
        if (!submission.Status.Equals("SUBMITTED", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Task {id} status must be SUBMITTED.");
        if (string.IsNullOrWhiteSpace(submission.TextResponse) && submission.Files.Count == 0) throw new ArgumentException($"Task {id} requires a text response or uploaded deliverable.");
        var allowed = planned.GetProperty("submissionTypes").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = allowed.Contains("TEXT") && !string.IsNullOrWhiteSpace(submission.TextResponse)
            || allowed.Contains("FILE") && submission.Files.Count > 0 || allowed.Contains("MULTI_FILE") && submission.Files.Count > 1
            || allowed.Contains("IMAGE") && submission.Files.Any(f => f.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            || allowed.Contains("PDF") && submission.Files.Any(f => Path.GetExtension(f.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            || allowed.Contains("EXCEL") && submission.Files.Any(f => new[] { ".xls", ".xlsx" }.Contains(Path.GetExtension(f.FileName), StringComparer.OrdinalIgnoreCase))
            || allowed.Contains("VIDEO") && submission.Files.Any(f => f.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase));
        if (!matches) throw new ArgumentException($"Task {id} requires one of these submission types: {string.Join(", ", allowed)}.");
    }
    private static JsonObject GetOrCreateResponseDay(JsonArray days, int dayNumber)
    {
        var day = days.OfType<JsonObject>().FirstOrDefault(d => d["day"]?.GetValue<int>() == dayNumber);
        if (day is not null) return day;
        day = new JsonObject { ["day"] = dayNumber, ["status"] = "IN_PROGRESS", ["tasks"] = new JsonArray() };
        days.Add(day); return day;
    }
    private static void UpsertTask(JsonObject day, string taskId, string status, string? text, List<OnboardingSubmissionFileDto> files, DateTime now)
    {
        var tasks = day["tasks"]!.AsArray();
        var existing = tasks.OfType<JsonObject>().FirstOrDefault(t => t["taskId"]?.GetValue<string>().Equals(taskId, StringComparison.OrdinalIgnoreCase) == true);
        var value = JsonSerializer.SerializeToNode(new { taskId, status, updatedAt = now, submittedAt = status == "SUBMITTED" ? now : (DateTime?)null, textResponse = text, files }, JsonOptions)!;
        if (existing is null) tasks.Add(value); else tasks[tasks.IndexOf(existing)] = value;
    }
    private static JsonElement GetSavedTasks(JsonElement responses, int dayNumber)
    {
        if (!responses.TryGetProperty("days", out var days)) return JsonDocument.Parse("[]").RootElement.Clone();
        var day = days.EnumerateArray().FirstOrDefault(d => d.GetProperty("day").GetInt32() == dayNumber);
        return day.ValueKind != JsonValueKind.Undefined && day.TryGetProperty("tasks", out var tasks) ? tasks.Clone() : JsonDocument.Parse("[]").RootElement.Clone();
    }
    private static bool Required(JsonElement item) => !item.TryGetProperty("required", out var value) || value.ValueKind != JsonValueKind.False;
    private static void ValidateAllRequiredTasksCompleted(OnboardingAssessment assessment)
    {
        var responseDays = assessment.ResponsesJson.RootElement.GetProperty("days");
        foreach (var planDay in assessment.PlanJson.RootElement.GetProperty("days").EnumerateArray())
        {
            var dayNumber = planDay.GetProperty("day").GetInt32();
            var responseDay = responseDays.EnumerateArray().FirstOrDefault(d => d.GetProperty("day").GetInt32() == dayNumber);
            if (responseDay.ValueKind == JsonValueKind.Undefined || !responseDay.TryGetProperty("tasks", out var submittedTasks))
                throw new InvalidOperationException($"Day {dayNumber} task submissions are incomplete.");
            var submittedIds = submittedTasks.EnumerateArray().Where(t =>
                    t.TryGetProperty("status", out var status) && status.GetString()?.Equals("SUBMITTED", StringComparison.OrdinalIgnoreCase) == true)
                .Select(t => t.GetProperty("taskId").GetString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var task in planDay.GetProperty("tasks").EnumerateArray().Where(Required))
                if (!submittedIds.Contains(task.GetProperty("taskId").GetString()))
                    throw new InvalidOperationException($"Required task {task.GetProperty("taskId").GetString()} has not been submitted.");
        }
    }
    private static JsonElement FindDay(JsonElement plan, int number)
    {
        foreach (var day in plan.GetProperty("days").EnumerateArray())
            if (day.TryGetProperty("day", out var value) && value.GetInt32() == number) return day;
        throw new ArgumentException("Day does not exist in the generated plan.");
    }
    private static JsonElement ArrayOrEmpty(JsonElement parent, string name) => parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array ? value.Clone() : JsonDocument.Parse("[]").RootElement.Clone();
    private static string? StringOrNull(JsonElement parent, string name) => parent.TryGetProperty(name, out var value) ? value.GetString() : null;
    private static bool Equal(string value, string expected) => value.Equals(expected, StringComparison.OrdinalIgnoreCase);
    private static OnboardingAssessmentResponseDto Map(OnboardingAssessment a) => new()
    {
        AssessmentId = a.Id, CandidateId = a.CandidateId, DurationDays = a.DurationDays, StartDate = a.StartDate,
        ExpectedEndDate = a.ExpectedEndDate, CurrentDay = a.CurrentDay, Status = a.Status,
        Plan = a.PlanJson.RootElement.Clone(), Responses = a.ResponsesJson.RootElement.Clone(), FinalEvaluation = a.FinalEvaluationJson?.RootElement.Clone()
    };
}
