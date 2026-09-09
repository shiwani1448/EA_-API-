using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Services;

public class AiScreeningService : IAiScreeningService
{
    private const int MinMeaningfulLength = DocumentExtractionConstants.MinMeaningfulTextLength;

    private static readonly string[] FinalStages =
        ["Rejected", "Offer", "Joined"];

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IDocumentExtractionService _extractor;
    private readonly IClaudeService _claude;
    private readonly IAiScreeningPromptBuilder _promptBuilder;
    private readonly ICandidateStatusService _statusService;
    private readonly IActivityService _activityService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiScreeningService> _logger;

    public AiScreeningService(
        AppDbContext db,
        IWebHostEnvironment env,
        IDocumentExtractionService extractor,
        IClaudeService claude,
        IAiScreeningPromptBuilder promptBuilder,
        ICandidateStatusService statusService,
        IActivityService activityService,
        IConfiguration configuration,
        ILogger<AiScreeningService> logger)
    {
        _db             = db;
        _env            = env;
        _extractor      = extractor;
        _claude         = claude;
        _promptBuilder  = promptBuilder;
        _statusService  = statusService;
        _activityService = activityService;
        _configuration  = configuration;
        _logger         = logger;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<AiScreeningBatchResponseDto> RunBatchAsync(AiScreeningBatchRequestDto request)
    {
        var candidateIds = await ResolveCandidateIdsAsync(request);

        var results = new List<AiScreeningResultItemDto>();

        foreach (var id in candidateIds)
        {
            try
            {
                var result = await ScreenCandidateAsync(id, request.ForceRescreen);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception screening candidate {Id}", id);
                var reason = $"Failed at Screening: {SafeExceptionMessage(ex)}";
                results.Add(new AiScreeningResultItemDto
                {
                    CandidateId    = id,
                    CandidateName  = "Unknown",
                    Status         = "Failed",
                    Reason         = reason
                });
            }
        }

        return BuildBatchResponse(results);
    }

    public async Task<AiScreeningResultItemDto> ScreenCandidateAsync(int candidateId, bool forceRescreen)
    {
        return await ScreenCandidateAsync(candidateId, forceRescreen, null);
    }

    public async Task<AiScreeningResultItemDto> ScreenCandidateAsync(
        int candidateId,
        bool forceRescreen,
        string? batchId,
        CancellationToken cancellationToken = default)
    {
        var screening = new CandidateAIScreening
        {
            CandidateId = candidateId,
            BatchId = batchId,
            CreatedAt = DateTime.UtcNow,
            StartedAt = DateTime.UtcNow,
            CurrentStep = "Candidate loading",
            ScreeningStatus = "Processing",
            AIStatus = "Not Started"
        };

        // ── 1. Load candidate ─────────────────────────────────────────────────

        Candidate? candidate;
        try
        {
            MarkStep(screening, "Candidate loading");
            candidate = await _db.Candidates
                .FirstOrDefaultAsync(c => c.CandidateId == candidateId && !c.IsDeleted, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed loading candidate {CandidateId}", candidateId);
            return Failed(candidateId, "Unknown", $"Failed at Candidate loading: {SafeExceptionMessage(ex)}");
        }

        if (candidate is null)
        {
            _logger.LogWarning("Candidate {Id} not found", candidateId);
            return Failed(candidateId, "Unknown", "Candidate not found.");
        }

        screening.RequisitionId = candidate.RequisitionId;
        screening.ResumePath    = candidate.ResumePath;
        screening.CandidateName  = candidate.FullName ?? "Unknown";

        // ── 2. Skip if already screened ───────────────────────────────────────

        if (!forceRescreen)
        {
            var hasCompleted = await _db.CandidateAIScreenings
                .AnyAsync(s => s.CandidateId == candidateId && s.Status == "Completed");

            if (hasCompleted)
            {
                var latest = await _db.CandidateAIScreenings
                .Where(s => s.CandidateId == candidateId && s.Status == "Completed")
                .OrderByDescending(s => s.CreatedAt)
                .FirstAsync(cancellationToken);

                return new AiScreeningResultItemDto
                {
                    CandidateId   = candidateId,
                    CandidateName = candidate.FullName ?? "Unknown",
                    Score         = latest.OverallScore,
                    Decision      = latest.Decision,
                    Recommendation = latest.Recommendation,
                    Status        = "Completed",
                    Reason        = "Already screened. Use forceRescreen=true to re-run."
                };
            }
        }

        // ── 3. Resume path & security ─────────────────────────────────────────

        if (string.IsNullOrWhiteSpace(candidate.ResumePath))
        {
            MarkStep(screening, "Resume file loading");
            return await PendingReviewAsync(screening, candidate, "Candidate has no resume on file.");
        }

        MarkStep(screening, "Resume file loading");
        var resumeFullPath = ResolveServerPath(candidate.ResumePath);

        if (!IsPathAllowed(resumeFullPath))
        {
            screening.FailureStep = "Resume file loading";
            return await PendingReviewAsync(screening, candidate, "Resume path failed security validation.");
        }

        if (!File.Exists(resumeFullPath))
        {
            screening.FailureStep = "Resume file loading";
            return await PendingReviewAsync(screening, candidate,
                $"Resume file not found on server path: {candidate.ResumePath}");
        }

        // ── 4. Load JD / Requisition ──────────────────────────────────────────

        MarkStep(screening, "JD file loading");
        var hiringRequest = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == candidate.RequisitionId && !h.IsDeleted, cancellationToken);

        if (hiringRequest is null)
        {
            screening.FailureStep = "JD file loading";
            return await PendingReviewAsync(screening, candidate, "Linked requisition not found.");
        }

        JDMaster? jdMaster = null;
        if (hiringRequest.JDID.HasValue)
        {
            jdMaster = await _db.JDMasters
                .FirstOrDefaultAsync(j => j.Id == hiringRequest.JDID.Value && !j.IsDeleted, cancellationToken);
        }

        // ── 5. Build JD text ──────────────────────────────────────────────────

        var jdText = CompileJDText(hiringRequest, jdMaster);
        screening.JDPath = jdMaster?.DocumentPdfPath;
        screening.JDExtractionStatus = jdText.Length >= MinMeaningfulLength
            ? DocumentExtractionConstants.StatusSuccess
            : DocumentExtractionConstants.StatusFailed;
        screening.JDExtractionMethod = jdText.Length >= MinMeaningfulLength
            ? DocumentExtractionConstants.MethodTextLayer
            : DocumentExtractionConstants.MethodFailed;
        screening.JDTextAvailable = jdText.Length >= MinMeaningfulLength;

        // If DB text is thin, try extracting from the JD PDF
        if (!string.IsNullOrWhiteSpace(jdMaster?.DocumentPdfPath))
        {
            var jdFilePath = ResolveServerPath(jdMaster!.DocumentPdfPath);
            if (IsPathAllowed(jdFilePath) && File.Exists(jdFilePath))
            {
                MarkStep(screening, "JD PDF extraction");
                DocumentExtractionResult jdExtraction;
                try
                {
                    jdExtraction = await WithTimeoutAsync(
                        ct => _extractor.ExtractAsync(jdFilePath, ct),
                        TimeSpan.FromSeconds(180),
                        cancellationToken,
                        "JD PDF extraction");
                }
                catch (Exception ex)
                {
                    CaptureException(screening, "JD PDF extraction", ex);
                    return await PendingReviewAsync(
                        screening,
                        candidate,
                        $"Failed at JD PDF extraction: {SafeExceptionMessage(ex)}",
                        cancellationToken: cancellationToken);
                }
                screening.JDExtractionMethod = jdExtraction.ExtractionMethod;
                screening.JDExtractionStatus = jdExtraction.ExtractionStatus;
                screening.JDTextAvailable = jdExtraction.TextAvailable || jdText.Length >= MinMeaningfulLength;
                ApplyExtractionDebug(screening, jdExtraction);

                if (jdExtraction.Success && !string.IsNullOrWhiteSpace(jdExtraction.Text))
                    jdText = jdText.Length > 0 ? jdText + "\n\n" + jdExtraction.Text : jdExtraction.Text;
                else if (jdText.Length < MinMeaningfulLength)
                    return await PendingReviewAsync(screening, candidate, BuildJdFailureReason(jdExtraction.FailureReason), cancellationToken: cancellationToken);
            }
            else if (!File.Exists(jdFilePath))
            {
                _logger.LogWarning("JD file not found on server path: {Path}", jdMaster!.DocumentPdfPath);
                if (jdText.Length < MinMeaningfulLength)
                    return await PendingReviewAsync(screening, candidate,
                        "JD text could not be extracted. JD PDF file is missing on the server.",
                        cancellationToken: cancellationToken);
            }
        }

        if (jdText.Length < MinMeaningfulLength)
        {
            screening.FailureStep = "Text length validation";
            return await PendingReviewAsync(screening, candidate,
                "JD text is too short. AI scoring skipped until the JD is reviewed.",
                cancellationToken: cancellationToken);
        }

        // ── 6. Extract resume text ────────────────────────────────────────────

        MarkStep(screening, "Resume PDF extraction");
        DocumentExtractionResult resumeResult;
        try
        {
            resumeResult = await WithTimeoutAsync(
                ct => _extractor.ExtractAsync(resumeFullPath, ct),
                TimeSpan.FromSeconds(240),
                cancellationToken,
                "Resume PDF extraction");
        }
        catch (Exception ex)
        {
            CaptureException(screening, "Resume PDF extraction", ex);
            return await PendingReviewAsync(
                screening,
                candidate,
                $"Failed at Resume PDF extraction: {SafeExceptionMessage(ex)}",
                cancellationToken: cancellationToken);
        }
        screening.ResumeExtractionMethod = resumeResult.ExtractionMethod;
        screening.ResumeExtractionStatus = resumeResult.ExtractionStatus;
        screening.ResumeTextAvailable = resumeResult.TextAvailable;
        ApplyExtractionDebug(screening, resumeResult);

        if (!resumeResult.Success)
        {
            return await PendingReviewAsync(screening, candidate, BuildResumeFailureReason(resumeResult.FailureReason), cancellationToken: cancellationToken);
        }

        var resumeText = resumeResult.Text ?? string.Empty;

        if (resumeText.Length < MinMeaningfulLength)
        {
            MarkStep(screening, "Text length validation");
            return await PendingReviewAsync(screening, candidate,
                "Resume text too short. AI scoring skipped until the resume is reviewed.",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Resume text extraction succeeded for candidate {Id}. Extracted length={Length}",
            candidateId,
            resumeText.Length);

        // ── 7. Build & send prompt ────────────────────────────────────────────

        MarkStep(screening, "AI/Claude request creation");
        var promptBuild = _promptBuilder.BuildCompressedPrompt(jdText, resumeText);
        screening.AiPromptLength = promptBuild.Prompt.Length;
        screening.JDExtractedText = promptBuild.CompressedJDText;
        screening.ResumeExtractedText = promptBuild.CompressedResumeText;

        _logger.LogInformation(
            "AI screening prompt prepared for candidate {Id}. JD text length={JDLength}, Resume text length={ResumeLength}, Final prompt length={PromptLength}",
            candidateId,
            screening.JDExtractedText.Length,
            screening.ResumeExtractedText.Length,
            promptBuild.Prompt.Length);

        string rawClaudeResponse;
        try
        {
            screening.AIStatus = "Processing";
            MarkStep(screening, "AI/Claude response receive");
            rawClaudeResponse = await GenerateWithRetryAsync(promptBuild.Prompt, candidateId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Claude call failed for candidate {Id}", candidateId);
            screening.AIStatus = "Failed";
            CaptureException(screening, "AI/Claude response receive", ex);
            return await PendingReviewAsync(screening, candidate,
                $"Failed at AI/Claude response receive: {SafeExceptionMessage(ex)}",
                cancellationToken: cancellationToken);
        }

        screening.RawClaudeResponse = rawClaudeResponse;
        screening.AiResponseLength = rawClaudeResponse.Length;

        // ── 8. Parse AI JSON ──────────────────────────────────────────────────

        MarkStep(screening, "AI response JSON parsing");
        var jsonStr   = ExtractJsonBlock(rawClaudeResponse);
        ClaudeAiResult? aiResult = null;

        if (jsonStr is not null)
        {
            aiResult = TryParseAiJson(jsonStr);

            if (aiResult is null)
            {
                // One repair attempt
                var repaired = RepairJson(jsonStr);
                aiResult = TryParseAiJson(repaired);
                if (aiResult is not null)
                    _logger.LogInformation("JSON repair succeeded for candidate {Id}", candidateId);
            }
        }

        if (aiResult is null)
        {
            screening.AIStatus = "Invalid Response";
            screening.FailureStep = "AI response JSON parsing";
            return await PendingReviewAsync(screening, candidate,
                "Failed at AI response parsing: invalid JSON returned by Claude",
                rawClaudeResponse,
                cancellationToken);
        }

        // ── 9. Map & save ─────────────────────────────────────────────────────

        MapAiResult(aiResult, screening);

        var decision = DetermineDecision(aiResult.OverallScore);
        screening.Decision  = decision;
        screening.Status    = "Completed";
        screening.ScreeningStatus = "Completed";
        screening.AIStatus = "Completed";
        screening.CompletedAt = DateTime.UtcNow;
        screening.DurationSeconds = CalculateDurationSeconds(screening);
        screening.UpdatedAt = DateTime.UtcNow;

        MarkStep(screening, "Database save/update");
        try
        {
            _db.CandidateAIScreenings.Add(screening);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            CaptureException(screening, "Database save/update", ex);
            _logger.LogError(ex, "Failed saving AI screening for candidate {CandidateId}", candidateId);
            return Failed(candidateId, candidate.FullName ?? "Unknown", $"Failed at database save: {SafeExceptionMessage(ex)}");
        }

        // ── 10. Update candidate status ───────────────────────────────────────

        try
        {
            if (decision == "Shortlist")
                await _statusService.ShortlistCandidateAsync(candidateId);
            else if (decision == "Reject")
                await _statusService.RejectCandidateAsync(candidateId);
            else
                await _statusService.MarkPendingReviewAsync(candidateId, aiResult.ShortSummary ?? decision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update status for candidate {Id} after AI screening", candidateId);
        }

        // ── 11. Post activity ─────────────────────────────────────────────────

        try
        {
            await _activityService.AddAiScreeningActivityAsync(
                candidateId, decision, aiResult.OverallScore, aiResult.ShortSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add AI screening activity for candidate {Id}", candidateId);
        }

        return new AiScreeningResultItemDto
        {
            CandidateId    = candidateId,
            CandidateName  = candidate.FullName ?? "Unknown",
            Score          = aiResult.OverallScore,
            Decision       = decision,
            Recommendation = aiResult.Recommendation,
            Status         = "Completed",
            Reason         = aiResult.ShortSummary
        };
    }

    // ── Candidate resolution ─────────────────────────────────────────────────

    private async Task<List<int>> ResolveCandidateIdsAsync(AiScreeningBatchRequestDto request)
    {
        if (request.CandidateIds is { Count: > 0 })
            return request.CandidateIds.Distinct().ToList();

        // Auto-select eligible candidates
        var query = _db.Candidates
            .Where(c => !c.IsDeleted
                && c.ResumePath != null && c.ResumePath != ""
                && c.RequisitionId > 0
                && !FinalStages.Contains(c.CurrentStage));

        if (!request.ForceRescreen)
        {
            var alreadyScreenedIds = await _db.CandidateAIScreenings
                .Where(s => s.Status == "Completed")
                .Select(s => s.CandidateId)
                .Distinct()
                .ToListAsync();

            query = query.Where(c => !alreadyScreenedIds.Contains(c.CandidateId));
        }

        return await query.Select(c => c.CandidateId).ToListAsync();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string ResolveServerPath(string relativePath)
    {
        var wwwRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        return Path.GetFullPath(
            Path.Combine(wwwRoot, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
    }

    private bool IsPathAllowed(string fullPath)
    {
        var wwwRoot    = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsDir = Path.GetFullPath(Path.Combine(wwwRoot, "uploads"));
        return fullPath.StartsWith(uploadsDir, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<AiScreeningResultItemDto> PendingReviewAsync(
        CandidateAIScreening screening,
        Candidate candidate,
        string reason,
        string? rawResponse = null,
        CancellationToken cancellationToken = default)
    {
        screening.ErrorMessage      = reason;
        screening.FailureReason     = reason;
        screening.Decision          = "Pending Review";
        screening.Status            = "Pending Review";
        screening.ScreeningStatus   = "Pending Review";
        screening.JDExtractionStatus ??= screening.JDTextAvailable
            ? DocumentExtractionConstants.StatusSuccess
            : DocumentExtractionConstants.StatusFailed;
        screening.ResumeExtractionStatus ??= screening.ResumeTextAvailable
            ? DocumentExtractionConstants.StatusSuccess
            : DocumentExtractionConstants.StatusFailed;
        screening.JDExtractionMethod ??= screening.JDTextAvailable
            ? DocumentExtractionConstants.MethodTextLayer
            : DocumentExtractionConstants.MethodFailed;
        screening.ResumeExtractionMethod ??= screening.ResumeTextAvailable
            ? DocumentExtractionConstants.MethodTextLayer
            : DocumentExtractionConstants.MethodFailed;
        screening.AIStatus          = string.IsNullOrWhiteSpace(screening.AIStatus) || screening.AIStatus == "Not Started"
            ? "Skipped"
            : screening.AIStatus;
        screening.RawClaudeResponse ??= rawResponse;
        screening.CompletedAt       = DateTime.UtcNow;
        screening.DurationSeconds   = CalculateDurationSeconds(screening);
        screening.UpdatedAt         = DateTime.UtcNow;

        await TrySaveScreeningAsync(screening, cancellationToken);

        try { await _statusService.MarkPendingReviewAsync(candidate.CandidateId, reason); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark candidate {Id} Pending Review", candidate.CandidateId);
        }

        try { await _activityService.AddAiScreeningActivityAsync(candidate.CandidateId, "Pending Review", null, reason); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add Pending Review activity for candidate {Id}", candidate.CandidateId);
        }

        return new AiScreeningResultItemDto
        {
            CandidateId   = candidate.CandidateId,
            CandidateName = candidate.FullName ?? "Unknown",
            Decision      = "Pending Review",
            Status        = "Pending Review",
            Reason        = reason
        };
    }

    private static AiScreeningResultItemDto Failed(int candidateId, string name, string reason) =>
        new()
        {
            CandidateId   = candidateId,
            CandidateName = name,
            Status        = "Failed",
            Reason        = reason
        };

    private async Task TrySaveScreeningAsync(
        CandidateAIScreening screening,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (screening.Id == 0)
                _db.CandidateAIScreenings.Add(screening);

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save CandidateAIScreening for candidate {Id}", screening.CandidateId);
        }
    }

    private static void MarkStep(CandidateAIScreening screening, string step)
    {
        screening.CurrentStep = step;
    }

    private static void CaptureException(CandidateAIScreening screening, string step, Exception exception)
    {
        screening.CurrentStep = step;
        screening.FailureStep = step;
        screening.ExceptionType = exception.GetType().Name;
        screening.ExceptionMessage = SafeExceptionMessage(exception);
        screening.InnerExceptionMessage = exception.InnerException?.Message;
    }

    private static string SafeExceptionMessage(Exception exception) =>
        string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;

    private static int CalculateDurationSeconds(CandidateAIScreening screening)
    {
        var startedAt = screening.StartedAt ?? screening.CreatedAt;
        var completedAt = screening.CompletedAt ?? DateTime.UtcNow;
        return Math.Max(0, (int)Math.Round((completedAt - startedAt).TotalSeconds));
    }

    private static async Task<T> WithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> action,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string step)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await action(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{step} timed out after {timeout.TotalSeconds:N0} seconds.");
        }
    }

    private static string CompileJDText(HiringRequest req, JDMaster? jd)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(req.Department))
            sb.AppendLine($"Department: {req.Department}");

        if (!string.IsNullOrWhiteSpace(req.Designation))
            sb.AppendLine($"Job Title: {req.Designation}");

        if (req.ExperienceMinYears.HasValue || req.ExperienceMaxYears.HasValue)
        {
            var exp = (req.ExperienceMinYears, req.ExperienceMaxYears) switch
            {
                ({ } min, { } max) => $"{min}-{max} years",
                ({ } min, null)    => $"Minimum {min} years",
                (null, { } max)    => $"Up to {max} years",
                _                  => string.Empty
            };
            if (!string.IsNullOrEmpty(exp))
                sb.AppendLine($"Experience Required: {exp}");
        }

        if (!string.IsNullOrWhiteSpace(req.ExperienceRequired))
            sb.AppendLine($"Experience Details: {req.ExperienceRequired}");

        if (req.BudgetMinLpa.HasValue || req.BudgetMaxLpa.HasValue)
        {
            var budget = (req.BudgetMinLpa, req.BudgetMaxLpa) switch
            {
                ({ } min, { } max) => $"{min}-{max} LPA",
                ({ } min, null)    => $"From {min} LPA",
                (null, { } max)    => $"Up to {max} LPA",
                _                  => string.Empty
            };
            if (!string.IsNullOrEmpty(budget))
                sb.AppendLine($"Budget: {budget}");
        }

        if (!string.IsNullOrWhiteSpace(req.AcceptableNoticePeriods))
            sb.AppendLine($"Acceptable Notice Periods: {req.AcceptableNoticePeriods}");

        if (jd is not null)
        {
            if (!string.IsNullOrWhiteSpace(jd.JobDescription))
                sb.AppendLine($"\nJob Description:\n{jd.JobDescription}");

            if (!string.IsNullOrWhiteSpace(jd.SkillsRequired))
                sb.AppendLine($"\nSkills Required:\n{jd.SkillsRequired}");

            if (!string.IsNullOrWhiteSpace(jd.Qualification))
                sb.AppendLine($"\nQualification:\n{jd.Qualification}");

            if (!string.IsNullOrWhiteSpace(jd.Experience))
                sb.AppendLine($"\nExperience:\n{jd.Experience}");
        }

        return sb.ToString().Trim();
    }

    // ── JSON Extraction & Parsing ─────────────────────────────────────────────

    private static string? ExtractJsonBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Strip markdown code fences
        text = Regex.Replace(text, @"```(?:json)?\s*", "", RegexOptions.IgnoreCase);

        // Find balanced outer JSON object
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return text[start..(i + 1)];
            }
        }

        return null;
    }

    private static ClaudeAiResult? TryParseAiJson(string jsonStr)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
            return JsonSerializer.Deserialize<ClaudeAiResult>(jsonStr, options);
        }
        catch
        {
            return null;
        }
    }

    private static string RepairJson(string json)
    {
        // Remove trailing commas before } or ]
        json = Regex.Replace(json, @",\s*([}\]])", "$1");
        return json;
    }

    private static string DetermineDecision(decimal? score) => score switch
    {
        >= 70 => "Shortlist",
        < 70  => "Reject",
        _     => "Pending Review"
    };

    private static void MapAiResult(ClaudeAiResult ai, CandidateAIScreening s)
    {
        s.OverallScore                = ai.OverallScore;
        s.Recommendation              = ai.Recommendation;
        s.ConfidenceScore             = ai.ConfidenceScore;
        s.RiskLevel                   = ai.RiskLevel;
        s.ShortSummary                = ai.ShortSummary;
        s.RoleFitScore                = ai.ScoreBreakdown?.RoleFit;
        s.SkillFitScore               = ai.ScoreBreakdown?.SkillFit;
        s.ExperienceRelevanceScore    = ai.ScoreBreakdown?.ExperienceRelevance;
        s.AchievementImpactScore      = ai.ScoreBreakdown?.AchievementImpact;
        s.CareerStabilityScore        = ai.ScoreBreakdown?.CareerStability;
        s.EducationCertificationScore = ai.ScoreBreakdown?.EducationCertification;
        s.IndustryAlignmentScore      = ai.ScoreBreakdown?.IndustryAlignment;
        s.GrowthPotentialScore        = ai.ScoreBreakdown?.GrowthPotential;
        s.RedFlagDeduction            = ai.ScoreBreakdown?.RedFlagDeduction;
        s.MatchedSkillsJson           = SerializeList(ai.MatchedSkills);
        s.MissingSkillsJson           = SerializeList(ai.MissingSkills);
        s.StrengthsJson               = SerializeList(ai.Strengths);
        s.ConcernsJson                = SerializeList(ai.Concerns);
        s.RedFlagsJson                = SerializeList(ai.RedFlags);
        s.InterviewFocusAreasJson     = SerializeList(ai.InterviewFocusAreas);
    }

    private static string? SerializeList(List<string>? list) =>
        list is { Count: > 0 } ? JsonSerializer.Serialize(list) : null;

    private static void ApplyExtractionDebug(CandidateAIScreening screening, DocumentExtractionResult extraction)
    {
        screening.NormalTextLength = Math.Max(screening.NormalTextLength, extraction.NormalTextLength);
        screening.PdfPageCount = Math.Max(screening.PdfPageCount, extraction.PdfPageCount);
        screening.RenderedImageCreated = screening.RenderedImageCreated || extraction.RenderedImageCreated;
        screening.RenderedImagePath ??= extraction.RenderedImagePath;
        screening.OCRTextLength = Math.Max(screening.OCRTextLength, extraction.OCRTextLength);
        screening.ExtractionMethodUsed = extraction.ExtractionMethodUsed ?? extraction.ExtractionMethod;
        screening.FailureStep = extraction.FailureStep;
    }

    private static string BuildOcrFailureReason(string? detail)
    {
        const string message = "Resume text could not be extracted even after OCR. Manual review required.";
        return string.IsNullOrWhiteSpace(detail) ? message : $"{message} {detail}";
    }

    private async Task<string> GenerateWithRetryAsync(
        string prompt,
        int candidateId,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Clamp(_configuration.GetValue("AnthropicSettings:MaxRetries", 2), 1, 3);
        Exception? lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await _claude.GenerateAsync(prompt, cancellationToken);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Claude attempt {Attempt}/{MaxAttempts} failed for candidate {CandidateId}",
                    attempt, maxAttempts, candidateId);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException("Claude request failed.");
    }

    private static string BuildResumeFailureReason(string? detail)
    {
        const string message = "Resume text could not be extracted. PDF may be scanned, blurred, protected, image-only, or low quality.";
        return string.IsNullOrWhiteSpace(detail) ? message : detail;
    }

    private static string BuildJdFailureReason(string? detail)
    {
        const string message = "JD text could not be extracted. PDF may be scanned, blurred, protected, image-only, or low quality.";
        return string.IsNullOrWhiteSpace(detail) ? message : detail;
    }

    private static AiScreeningBatchResponseDto BuildBatchResponse(List<AiScreeningResultItemDto> results)
    {
        var shortlisted   = results.Count(r => r.Decision == "Shortlist");
        var rejected      = results.Count(r => r.Decision == "Reject");
        var pendingReview = results.Count(r => r.Status == "Pending Review");
        var failed        = results.Count(r => r.Status == "Failed");
        var completed     = results.Count(r => r.Status == "Completed");

        return new AiScreeningBatchResponseDto
        {
            Success      = true,
            Message      = "AI screening batch completed.",
            Total        = results.Count,
            Completed    = completed,
            Shortlisted  = shortlisted,
            Rejected     = rejected,
            PendingReview = pendingReview,
            Failed       = failed,
            Results      = results
        };
    }

    // ── Private AI response model ─────────────────────────────────────────────

    private sealed class ClaudeAiResult
    {
        [JsonPropertyName("overallScore")]      public decimal? OverallScore   { get; set; }
        [JsonPropertyName("recommendation")]    public string?  Recommendation { get; set; }
        [JsonPropertyName("decision")]          public string?  Decision       { get; set; }
        [JsonPropertyName("confidenceScore")]   public decimal? ConfidenceScore { get; set; }
        [JsonPropertyName("riskLevel")]         public string?  RiskLevel      { get; set; }
        [JsonPropertyName("scoreBreakdown")]    public ScoreBreakdown? ScoreBreakdown { get; set; }
        [JsonPropertyName("matchedSkills")]     public List<string>? MatchedSkills      { get; set; }
        [JsonPropertyName("missingSkills")]     public List<string>? MissingSkills      { get; set; }
        [JsonPropertyName("strengths")]         public List<string>? Strengths           { get; set; }
        [JsonPropertyName("concerns")]          public List<string>? Concerns            { get; set; }
        [JsonPropertyName("redFlags")]          public List<string>? RedFlags            { get; set; }
        [JsonPropertyName("interviewFocusAreas")] public List<string>? InterviewFocusAreas { get; set; }
        [JsonPropertyName("shortSummary")]      public string?  ShortSummary   { get; set; }
    }

    private sealed class ScoreBreakdown
    {
        [JsonPropertyName("roleFit")]                 public decimal? RoleFit                { get; set; }
        [JsonPropertyName("skillFit")]                public decimal? SkillFit               { get; set; }
        [JsonPropertyName("experienceRelevance")]     public decimal? ExperienceRelevance    { get; set; }
        [JsonPropertyName("achievementImpact")]       public decimal? AchievementImpact      { get; set; }
        [JsonPropertyName("careerStability")]         public decimal? CareerStability        { get; set; }
        [JsonPropertyName("educationCertification")]  public decimal? EducationCertification { get; set; }
        [JsonPropertyName("industryAlignment")]       public decimal? IndustryAlignment      { get; set; }
        [JsonPropertyName("growthPotential")]         public decimal? GrowthPotential        { get; set; }
        [JsonPropertyName("redFlagDeduction")]        public decimal? RedFlagDeduction       { get; set; }
    }
}



