using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Services;

public class AssessmentEvaluationService : IAssessmentEvaluationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IDocumentExtractionService _documentExtractionService;
    private readonly IClaudeService _claudeService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AssessmentEvaluationService> _logger;

    public AssessmentEvaluationService(
        AppDbContext db,
        IWebHostEnvironment env,
        IDocumentExtractionService documentExtractionService,
        IClaudeService claudeService,
        IConfiguration configuration,
        ILogger<AssessmentEvaluationService> logger)
    {
        _db = db;
        _env = env;
        _documentExtractionService = documentExtractionService;
        _claudeService = claudeService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AssessmentEvaluationResponseDto> EvaluateAndSaveAsync(
        AssessmentEvaluateAndSaveRequestDto request,
        IFormFileCollection formFiles,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateRequest(request);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));

        var candidate = await _db.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CandidateId == request.CandidateId && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            throw new KeyNotFoundException("Candidate not found.");

        var questions = DeserializeQuestions(request.QuestionsJson);
        if (questions.Count == 0)
            throw new ArgumentException("Questions are required.");

        var context = new AssessmentEvaluationContext(
            request.CandidateId,
            request.InterviewId,
            request.RoundId,
            NormalizeRequired(request.Department),
            NormalizeRequired(request.Designation),
            NormalizeRequired(request.Level),
            NormalizeRequired(request.RoundName),
            request.CreatedBy);

        var roundExists = await _db.AssessmentQuestionBanks
            .AsNoTracking()
            .AnyAsync(q => q.IsActive
                && q.Department.ToLower() == context.Department.ToLower()
                && q.Designation.ToLower() == context.Designation.ToLower()
                && q.Level.ToLower() == context.Level.ToLower()
                && q.RoundName.ToLower() == context.RoundName.ToLower(),
                cancellationToken);

        if (!roundExists)
            throw new KeyNotFoundException("Assessment round/question bank not found for the supplied department, designation, level, and round name.");

        _logger.LogInformation("Assessment evaluation FILES COUNT={FilesCount}", formFiles.Count);
        foreach (var formFile in formFiles)
        {
            _logger.LogInformation(
                "Assessment evaluation FORM FILE KEY={FormFileKey} FILE NAME={FileName} FILE SIZE={FileSize}",
                formFile.Name,
                formFile.FileName,
                formFile.Length);
        }

        var filesByName = formFiles
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var unnamedFiles = formFiles.Where(f => string.Equals(f.Name, "files", StringComparison.OrdinalIgnoreCase)).ToList();
        var practicalIndex = 0;
        var questionDetails = new List<AssessmentQuestionEvaluationDetail>();
        var textModel = GetTextModel();

        foreach (var question in questions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var detail = new AssessmentQuestionEvaluationDetail
            {
                QuestionId = question.QuestionId,
                QuestionText = NormalizeRequired(question.QuestionText),
                QuestionType = NormalizeRequired(question.QuestionType),
                Category = NormalizeOptional(question.Category),
                Difficulty = NormalizeOptional(question.Difficulty),
                AnswerText = NormalizeOptional(question.AnswerText),
                Remark = NormalizeOptional(question.Remark),
                MaxMarks = question.MaxMarks > 0 ? question.MaxMarks : 0,
                ObtainedMarks = 0,
                AiFeedback = string.Empty,
                AiDecision = "Pending",
                ManualReviewRequired = false,
                FileKey = NormalizeOptional(question.FileKey ?? question.FileFieldName),
                FileReceived = false,
                FileMatched = false,
                ModelUsed = textModel
            };

            if (IsPractical(detail.QuestionType))
            {
                var resolvedFile = ResolveQuestionFile(question, detail.QuestionId, filesByName, unnamedFiles, practicalIndex);
                var file = resolvedFile.File;
                practicalIndex++;

                detail.FileMatched = file is not null;
                detail.FileReceived = file is not null && file.Length > 0;

                _logger.LogInformation(
                    "Assessment practical QUESTION ID={QuestionId} QUESTION FILE KEY={QuestionFileKey} MATCHED FILE {MatchedStatus} MATCHED FILE KEY={MatchedFileKey}",
                    detail.QuestionId,
                    detail.FileKey,
                    file is null ? "NOT FOUND" : "FOUND",
                    resolvedFile.MatchedKey);

                if (file is null || file.Length == 0)
                {
                    if (file is not null)
                    {
                        detail.UploadedFileName = SafeFileName(file.FileName);
                        detail.UploadedFileSize = file.Length;
                    }

                    detail.ManualReviewRequired = true;
                    detail.AiDecision = "Manual Review Required";
                    detail.AiFeedback = "Practical document was not uploaded or was empty.";
                    detail.EvaluationLocked = true;
                }
                else
                {
                    await SaveAndEvaluatePracticalFileAsync(detail, file, cancellationToken);
                }
            }

            questionDetails.Add(detail);
        }

        var totalMarks = questionDetails.Sum(q => q.MaxMarks);
        var requiresTextEvaluation = questionDetails.Any(q => !q.EvaluationLocked);
        var aiResponse = requiresTextEvaluation
            ? await GenerateAndParseAsync(BuildPrompt(context, candidate, questionDetails, totalMarks), cancellationToken)
            : new AssessmentAiCallResult(
                true,
                string.Empty,
                new AssessmentAiResponse
                {
                    Recommendation = "Assessment evaluation saved.",
                    Strengths = new List<string>(),
                    Weaknesses = new List<string>(),
                    ManualReviewRequired = questionDetails.Any(q => q.ManualReviewRequired)
                },
                null);
        var applied = ApplyAiResponse(questionDetails, aiResponse.Parsed, totalMarks);
        var extractionNeedsReview = questionDetails.Any(q => q.ManualReviewRequired);
        var invalidAi = !aiResponse.IsValid;
        var manualReviewRequired = extractionNeedsReview || applied.ManualReviewRequired || invalidAi;
        var result = invalidAi
            ? "Pending Review"
            : DetermineResult(applied.Percentage, manualReviewRequired && applied.Percentage >= 50);

        var evaluationJsonPayload = new
        {
            rawAiResponse = aiResponse.Raw,
            aiResponseValid = aiResponse.IsValid,
            aiParseError = aiResponse.ParseError,
            questions = questionDetails
        };

        var evaluation = new AssessmentEvaluation
        {
            CandidateId = context.CandidateId,
            InterviewId = context.InterviewId ?? context.RoundId,
            RoundId = context.RoundId ?? context.InterviewId,
            Department = context.Department,
            Designation = context.Designation,
            Level = context.Level,
            RoundName = context.RoundName,
            TotalMarks = totalMarks,
            ObtainedMarks = applied.ObtainedMarks,
            Percentage = applied.Percentage,
            Result = result,
            Recommendation = applied.Recommendation,
            AiSummary = BuildSummary(applied, invalidAi),
            Strengths = SerializeStringList(applied.Strengths),
            Weaknesses = SerializeStringList(applied.Weaknesses),
            ManualReviewRequired = manualReviewRequired,
            EvaluationJson = ToJsonDocument(evaluationJsonPayload),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = NormalizeOptional(context.CreatedBy)
        };

        _db.AssessmentEvaluations.Add(evaluation);
        await _db.SaveChangesAsync(cancellationToken);

        return MapToResponse(evaluation);
    }

    public async Task<List<AssessmentEvaluationWithCandidateResponseDto>> GetAllAsync(
        int? candidateId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AssessmentEvaluations
            .AsNoTracking()
            .Include(e => e.Candidate)
            .AsQueryable();

        if (candidateId.HasValue)
            query = query.Where(e => e.CandidateId == candidateId.Value);

        var evaluations = await query
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return evaluations.Select(MapToResponseWithCandidate).ToList();
    }

    public async Task<AssessmentEvaluationWithCandidateResponseDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await _db.AssessmentEvaluations
            .AsNoTracking()
            .Include(e => e.Candidate)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        return evaluation is null ? null : MapToResponseWithCandidate(evaluation);
    }

    public async Task<AssessmentEvaluationWithCandidateResponseDto?> GetLatestByCandidateIdAsync(
        int candidateId,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await _db.AssessmentEvaluations
            .AsNoTracking()
            .Include(e => e.Candidate)
            .Where(e => e.CandidateId == candidateId)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return evaluation is null ? null : MapToResponseWithCandidate(evaluation);
    }

    private async Task SaveAndEvaluatePracticalFileAsync(
        AssessmentQuestionEvaluationDetail detail,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var uploadsDir = Path.Combine(
            _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
            "uploads",
            "assessment-practical-documents");
        Directory.CreateDirectory(uploadsDir);

        var safeFileName = SafeFileName(file.FileName) ?? "document";
        var uniqueName = $"{Guid.NewGuid()}_{safeFileName}";
        var fullPath = Path.Combine(uploadsDir, uniqueName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        detail.UploadedFileName = safeFileName;
        detail.UploadedFileSize = file.Length;
        detail.UploadedFilePath = $"/uploads/assessment-practical-documents/{uniqueName}";

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (IsImageFile(extension))
        {
            await EvaluatePracticalImageAsync(detail, fullPath, cancellationToken);
            return;
        }

        try
        {
            var extraction = await _documentExtractionService.ExtractAsync(fullPath, cancellationToken);
            detail.ExtractionMethod = extraction.ExtractionMethodUsed ?? extraction.ExtractionMethod;
            detail.ExtractionFailureReason = extraction.FailureReason;
            detail.ExtractedDocumentText = extraction.Text;
            detail.DocumentMetadata = new
            {
                extraction.ExtractionStatus,
                extraction.TextAvailable,
                extraction.IsSelectableTextPdf,
                extraction.IsImageBasedPdf,
                extraction.NormalTextLength,
                extraction.PdfPageCount,
                extraction.RenderedImageCreated,
                extraction.OCRTextLength,
                extraction.FailureStep
            };

            if (!extraction.Success || string.IsNullOrWhiteSpace(extraction.Text))
            {
                detail.ManualReviewRequired = true;
                detail.AiDecision = "Manual Review Required";
                detail.AiFeedback = IsPdfFile(extension)
                    ? "PDF uploaded but visual extraction is not supported yet. Manual review required."
                    : "File uploaded successfully but AI could not evaluate it. Manual review required.";
                detail.EvaluationLocked = IsPdfFile(extension);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Practical document extraction failed for {FileName}", safeFileName);
            detail.ManualReviewRequired = true;
            detail.AiDecision = "Manual Review Required";
            detail.AiFeedback = "File uploaded successfully but AI could not evaluate it. Manual review required.";
            detail.ExtractionFailureReason = ex.Message;
            detail.EvaluationLocked = true;
        }
    }

    private async Task EvaluatePracticalImageAsync(
        AssessmentQuestionEvaluationDetail detail,
        string fullPath,
        CancellationToken cancellationToken)
    {
        detail.ModelUsed = GetVisionModel();
        var prompt = BuildVisionPrompt(detail);

        try
        {
            var imageBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(fullPath, cancellationToken));
            var raw = await _claudeService.GenerateVisionAsync(prompt, imageBase64, cancellationToken);
            detail.RawAiResponse = raw;

            if (!TryParseVisionResponse(raw, out var parsed, out var parseError) || parsed is null)
            {
                detail.ManualReviewRequired = true;
                detail.AiDecision = "Manual Review Required";
                detail.AiFeedback = "File uploaded successfully but AI could not evaluate it. Manual review required.";
                detail.ExtractionFailureReason = parseError;
                detail.EvaluationLocked = true;
                return;
            }

            detail.ObtainedMarks = Clamp(parsed.ObtainedMarks, 0, detail.MaxMarks > 0 ? detail.MaxMarks : 10);
            detail.AiDecision = NormalizeOptional(parsed.AiDecision) ?? "Manual Review Required";
            detail.AiFeedback = NormalizeOptional(parsed.AiFeedback) ?? "Image evaluated successfully.";
            detail.ManualReviewRequired = parsed.ManualReviewRequired;
            detail.EvaluationLocked = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException or JsonException)
        {
            _logger.LogWarning(ex, "Practical image vision evaluation failed for question {QuestionId} file {FileName}",
                detail.QuestionId,
                detail.UploadedFileName);
            detail.ManualReviewRequired = true;
            detail.AiDecision = "Manual Review Required";
            detail.AiFeedback = "File uploaded successfully but AI could not evaluate it. Manual review required.";
            detail.ExtractionFailureReason = ex.Message;
            detail.EvaluationLocked = true;
        }
    }

    private async Task<AssessmentAiCallResult> GenerateAndParseAsync(string prompt, CancellationToken cancellationToken)
    {
        string? raw = null;
        string? parseError = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            raw = await _claudeService.GenerateAsync(
                attempt == 1 ? prompt : prompt + "\n\nYour previous response was invalid. Return valid JSON only, with no markdown or explanation.",
                cancellationToken);

            if (TryParseAiResponse(raw, out var parsed, out parseError))
                return new AssessmentAiCallResult(true, raw, parsed, null);
        }

        return new AssessmentAiCallResult(false, raw ?? string.Empty, null, parseError);
    }

    private static bool TryParseAiResponse(string raw, out AssessmentAiResponse? parsed, out string? parseError)
    {
        parsed = null;
        parseError = null;

        try
        {
            var json = ExtractJsonObject(raw);
            parsed = JsonSerializer.Deserialize<AssessmentAiResponse>(json, JsonOptions);
            if (parsed is null)
            {
                parseError = "AI response JSON was empty.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            parseError = ex.Message;
            return false;
        }
    }

    private static bool TryParseVisionResponse(string raw, out AssessmentVisionResponse? parsed, out string? parseError)
    {
        parsed = null;
        parseError = null;

        try
        {
            var json = ExtractJsonObject(raw);
            parsed = JsonSerializer.Deserialize<AssessmentVisionResponse>(json, JsonOptions);
            if (parsed is null)
            {
                parseError = "AI vision response JSON was empty.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            parseError = ex.Message;
            return false;
        }
    }

    private static string ExtractJsonObject(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new ArgumentException("AI response did not contain a JSON object.");

        return trimmed[start..(end + 1)];
    }

    private static AppliedAssessmentResult ApplyAiResponse(
        List<AssessmentQuestionEvaluationDetail> details,
        AssessmentAiResponse? ai,
        decimal totalMarks)
    {
        if (ai?.Questions is not null)
        {
            foreach (var aiQuestion in ai.Questions)
            {
                var detail = details.FirstOrDefault(q => q.QuestionId == aiQuestion.QuestionId);
                if (detail is null)
                    continue;
                if (detail.EvaluationLocked)
                    continue;

                detail.ObtainedMarks = Clamp(aiQuestion.ObtainedMarks, 0, detail.MaxMarks);
                detail.AiFeedback = NormalizeOptional(aiQuestion.AiFeedback) ?? detail.AiFeedback;
                detail.AiDecision = NormalizeOptional(aiQuestion.AiDecision) ?? detail.AiDecision;
                detail.ManualReviewRequired = detail.ManualReviewRequired || aiQuestion.ManualReviewRequired;
            }
        }

        foreach (var detail in details.Where(q => q.ManualReviewRequired && string.IsNullOrWhiteSpace(q.AiFeedback)))
        {
            detail.AiFeedback = "Manual review is required for this answer.";
            detail.AiDecision = "Manual Review Required";
        }

        var obtained = Clamp(details.Sum(q => q.ObtainedMarks), 0, totalMarks);
        var percentage = totalMarks <= 0 ? 0 : Math.Round(obtained / totalMarks * 100, 2);

        return new AppliedAssessmentResult(
            obtained,
            percentage,
            ai?.Recommendation ?? "Assessment evaluation saved.",
            ai?.Strengths?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>(),
            ai?.Weaknesses?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList() ?? new List<string>(),
            ai?.ManualReviewRequired ?? false);
    }

    private static string BuildPrompt(
        AssessmentEvaluationContext context,
        Candidate candidate,
        IReadOnlyList<AssessmentQuestionEvaluationDetail> questions,
        decimal totalMarks)
    {
        var payload = new
        {
            candidate = new
            {
                candidate.CandidateId,
                candidate.FullName,
                candidate.Email,
                candidate.YearsOfExperience,
                candidate.KeySkills,
                candidate.CurrentStage,
                candidate.CurrentStatus
            },
            jobContext = new
            {
                context.Department,
                context.Designation,
                context.Level,
                context.RoundName
            },
            scoringRules = new
            {
                totalMarks,
                pass = "70% and above",
                pendingReview = "50% to 69% or any material manual review concern",
                fail = "Below 50%"
            },
            questions
        };

        var sb = new StringBuilder();
        sb.AppendLine("You are an expert technical/assessment evaluator for HRMS.");
        sb.AppendLine("Evaluate questionnaire answers and practical uploaded-document evidence against the question, category/skill area, difficulty, department, designation, and level.");
        sb.AppendLine("Check relevance, completeness, practicality, realism, job-level fit, whether practical documents satisfy the requirement, and red flags such as copied, empty, vague, or irrelevant answers.");
        sb.AppendLine("Some practical image questions may already have a modelUsed vision result. Keep those question scores and decisions unchanged.");
        sb.AppendLine("For questions already marked manualReviewRequired, keep aiDecision as Manual Review Required unless the provided text is enough to score confidently.");
        sb.AppendLine("Use maxMarks per question. Do not exceed maxMarks. Final result must be based on percentage: 70 and above Pass, 50 to 69 Pending Review, below 50 Fail.");
        sb.AppendLine("Return JSON only. No markdown. No explanation outside JSON.");
        sb.AppendLine("Required response schema:");
        sb.AppendLine("""
{
  "totalMarks": number,
  "obtainedMarks": number,
  "percentage": number,
  "result": "Pass | Fail | Pending Review",
  "recommendation": "string",
  "strengths": ["string"],
  "weaknesses": ["string"],
  "manualReviewRequired": true,
  "questions": [
    {
      "questionId": number,
      "obtainedMarks": number,
      "aiFeedback": "string",
      "aiDecision": "Correct | Partially Correct | Incorrect | Manual Review Required",
      "manualReviewRequired": true
    }
  ]
}
""");
        sb.AppendLine("Assessment input JSON:");
        sb.AppendLine(JsonSerializer.Serialize(payload, JsonOptions));
        return sb.ToString();
    }

    private static List<string> ValidateRequest(AssessmentEvaluateAndSaveRequestDto request)
    {
        var errors = new List<string>();
        if (request.CandidateId <= 0)
            errors.Add("CandidateId is required.");
        if (request.InterviewId is null && request.RoundId is null)
            errors.Add("InterviewId or RoundId is required.");
        AddRequired(errors, request.Department, "Department");
        AddRequired(errors, request.Designation, "Designation");
        AddRequired(errors, request.Level, "Level");
        AddRequired(errors, request.RoundName, "RoundName");
        if (string.IsNullOrWhiteSpace(request.QuestionsJson))
            errors.Add("Questions JSON is required in form field 'questions'.");
        return errors;
    }

    private static List<AssessmentEvaluationQuestionInputDto> DeserializeQuestions(string? questionsJson)
    {
        if (string.IsNullOrWhiteSpace(questionsJson))
            return new List<AssessmentEvaluationQuestionInputDto>();

        return JsonSerializer.Deserialize<List<AssessmentEvaluationQuestionInputDto>>(questionsJson, JsonOptions)
            ?? new List<AssessmentEvaluationQuestionInputDto>();
    }

    private static (IFormFile? File, string? MatchedKey) ResolveQuestionFile(
        AssessmentEvaluationQuestionInputDto question,
        int questionId,
        Dictionary<string, IFormFile> filesByName,
        IReadOnlyList<IFormFile> unnamedFiles,
        int practicalIndex)
    {
        var keys = new[]
        {
            question.FileKey,
            question.FileFieldName,
            $"questionFiles[{questionId}]",
            $"files[{questionId}]",
            $"files_q_{questionId}",
            $"file_{questionId}",
            $"question_{questionId}",
            questionId.ToString()
        };

        foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            if (filesByName.TryGetValue(key!, out var file))
                return (file, key);
        }

        return practicalIndex >= 0 && practicalIndex < unnamedFiles.Count
            ? (unnamedFiles[practicalIndex], unnamedFiles[practicalIndex].Name)
            : (null, null);
    }

    private string GetTextModel() =>
        _configuration["AnthropicSettings:TextModel"]
        ?? _configuration["AnthropicSettings:Model"]
        ?? "claude-sonnet-5";

    private string GetVisionModel() =>
        _configuration["AnthropicSettings:VisionModel"] ?? "claude-sonnet-5";

    private string BuildVisionPrompt(AssessmentQuestionEvaluationDetail detail)
    {
        var maxMarks = detail.MaxMarks > 0 ? detail.MaxMarks : 10;
        var sb = new StringBuilder();
        sb.AppendLine("You are an assessment evaluator.");
        sb.AppendLine("Candidate was asked:");
        sb.AppendLine($"\"{detail.QuestionText}\"");
        sb.AppendLine();
        sb.AppendLine("Evaluate the uploaded image carefully.");
        sb.AppendLine();
        sb.AppendLine("Scoring:");
        sb.AppendLine($"- Award marks from 0 to {maxMarks:0.##} based on correctness, completeness, relevance, and clarity.");
        sb.AppendLine("- If the drawing or written work clearly satisfies the task, give high marks.");
        sb.AppendLine("- If it partially satisfies the task, give partial marks.");
        sb.AppendLine("- If it is irrelevant, blank, unreadable, or impossible to judge, require manual review.");
        sb.AppendLine();
        sb.AppendLine("Return JSON only:");
        sb.AppendLine($$"""
{
  "obtainedMarks": 0-{{maxMarks:0.##}},
  "aiDecision": "Correct | Partially Correct | Incorrect | Manual Review Required",
  "aiFeedback": "short feedback",
  "manualReviewRequired": false
}
""");
        return sb.ToString();
    }

    private static bool IsPractical(string questionType) =>
        questionType.Contains("practical", StringComparison.OrdinalIgnoreCase);

    private static bool IsPdfFile(string extension) =>
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsImageFile(string extension) =>
        extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".tif" or ".tiff";

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(max, Math.Max(min, value));

    private static string DetermineResult(decimal percentage, bool hasManualReviewConcern)
    {
        if (percentage >= 70 && !hasManualReviewConcern)
            return "Pass";
        if (percentage >= 50)
            return "Pending Review";
        return "Fail";
    }

    private static string BuildSummary(AppliedAssessmentResult result, bool invalidAi)
    {
        if (invalidAi)
            return "AI response could not be parsed after retry. Evaluation saved for pending review.";

        return $"Obtained {result.ObtainedMarks:0.##} marks ({result.Percentage:0.##}%). {result.Recommendation}";
    }

    private static AssessmentEvaluationResponseDto MapToResponse(AssessmentEvaluation evaluation) => new()
    {
        Id = evaluation.Id,
        CandidateId = evaluation.CandidateId,
        InterviewId = evaluation.InterviewId,
        RoundId = evaluation.RoundId,
        Department = evaluation.Department,
        Designation = evaluation.Designation,
        Level = evaluation.Level,
        RoundName = evaluation.RoundName,
        TotalMarks = evaluation.TotalMarks,
        ObtainedMarks = evaluation.ObtainedMarks,
        Percentage = evaluation.Percentage,
        Result = evaluation.Result,
        Recommendation = evaluation.Recommendation,
        AiSummary = evaluation.AiSummary,
        Strengths = DeserializeStringList(evaluation.Strengths),
        Weaknesses = DeserializeStringList(evaluation.Weaknesses),
        ManualReviewRequired = evaluation.ManualReviewRequired,
        EvaluationJson = evaluation.EvaluationJson,
        CreatedAt = evaluation.CreatedAt,
        CreatedBy = evaluation.CreatedBy
    };

    private static AssessmentEvaluationWithCandidateResponseDto MapToResponseWithCandidate(AssessmentEvaluation evaluation) => new()
    {
        Id = evaluation.Id,
        CandidateId = evaluation.CandidateId,
        InterviewId = evaluation.InterviewId,
        RoundId = evaluation.RoundId,
        Department = evaluation.Department,
        Designation = evaluation.Designation,
        Level = evaluation.Level,
        RoundName = evaluation.RoundName,
        TotalMarks = evaluation.TotalMarks,
        ObtainedMarks = evaluation.ObtainedMarks,
        Percentage = evaluation.Percentage,
        Result = evaluation.Result,
        Recommendation = evaluation.Recommendation,
        AiSummary = evaluation.AiSummary,
        Strengths = DeserializeStringList(evaluation.Strengths),
        Weaknesses = DeserializeStringList(evaluation.Weaknesses),
        ManualReviewRequired = evaluation.ManualReviewRequired,
        EvaluationJson = evaluation.EvaluationJson,
        CreatedAt = evaluation.CreatedAt,
        CreatedBy = evaluation.CreatedBy,
        Candidate = evaluation.Candidate is null ? null : new AssessmentEvaluationCandidateDto
        {
            CandidateId = evaluation.Candidate.CandidateId,
            FullName = evaluation.Candidate.FullName,
            Email = evaluation.Candidate.Email,
            PhoneNumber = evaluation.Candidate.PhoneNumber,
            YearsOfExperience = evaluation.Candidate.YearsOfExperience,
            KeySkills = evaluation.Candidate.KeySkills,
            Source = evaluation.Candidate.Source,
            CurrentStage = evaluation.Candidate.CurrentStage,
            CurrentStatus = evaluation.Candidate.CurrentStatus
        }
    };

    private static JsonDocument ToJsonDocument(object value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));

    private static string SerializeStringList(List<string> values) =>
        JsonSerializer.Serialize(values, JsonOptions);

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    private static void AddRequired(List<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{fieldName} is required.");
    }

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SafeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var safeName = Path.GetFileName(fileName.Trim());
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(invalidChar, '_');

        return safeName;
    }

    private sealed record AssessmentEvaluationContext(
        int CandidateId,
        int? InterviewId,
        int? RoundId,
        string Department,
        string Designation,
        string Level,
        string RoundName,
        string? CreatedBy);

    private sealed record AssessmentAiCallResult(
        bool IsValid,
        string Raw,
        AssessmentAiResponse? Parsed,
        string? ParseError);

    private sealed record AppliedAssessmentResult(
        decimal ObtainedMarks,
        decimal Percentage,
        string Recommendation,
        List<string> Strengths,
        List<string> Weaknesses,
        bool ManualReviewRequired);

    private sealed class AssessmentAiResponse
    {
        public decimal TotalMarks { get; set; }
        public decimal ObtainedMarks { get; set; }
        public decimal Percentage { get; set; }
        public string? Result { get; set; }
        public string? Recommendation { get; set; }
        public List<string>? Strengths { get; set; }
        public List<string>? Weaknesses { get; set; }
        public bool ManualReviewRequired { get; set; }
        public List<AssessmentAiQuestionResponse>? Questions { get; set; }
    }

    private sealed class AssessmentAiQuestionResponse
    {
        public int QuestionId { get; set; }
        public decimal ObtainedMarks { get; set; }
        public string? AiFeedback { get; set; }
        public string? AiDecision { get; set; }
        public bool ManualReviewRequired { get; set; }
    }

    private sealed class AssessmentVisionResponse
    {
        public decimal ObtainedMarks { get; set; }
        public string? AiDecision { get; set; }
        public string? AiFeedback { get; set; }
        public bool ManualReviewRequired { get; set; }
    }

    private sealed class AssessmentQuestionEvaluationDetail
    {
        [JsonPropertyName("questionId")]
        public int QuestionId { get; set; }

        [JsonPropertyName("questionText")]
        public string QuestionText { get; set; } = string.Empty;

        [JsonPropertyName("questionType")]
        public string QuestionType { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("difficulty")]
        public string? Difficulty { get; set; }

        [JsonPropertyName("answerText")]
        public string? AnswerText { get; set; }

        [JsonPropertyName("remark")]
        public string? Remark { get; set; }

        [JsonPropertyName("fileKey")]
        public string? FileKey { get; set; }

        [JsonPropertyName("uploadedFileName")]
        public string? UploadedFileName { get; set; }

        [JsonPropertyName("uploadedFileSize")]
        public long? UploadedFileSize { get; set; }

        [JsonPropertyName("fileReceived")]
        public bool FileReceived { get; set; }

        [JsonPropertyName("fileMatched")]
        public bool FileMatched { get; set; }

        [JsonPropertyName("uploadedFilePath")]
        public string? UploadedFilePath { get; set; }

        [JsonPropertyName("extractedDocumentText")]
        public string? ExtractedDocumentText { get; set; }

        [JsonPropertyName("maxMarks")]
        public decimal MaxMarks { get; set; }

        [JsonPropertyName("obtainedMarks")]
        public decimal ObtainedMarks { get; set; }

        [JsonPropertyName("aiFeedback")]
        public string AiFeedback { get; set; } = string.Empty;

        [JsonPropertyName("aiDecision")]
        public string AiDecision { get; set; } = string.Empty;

        [JsonPropertyName("manualReviewRequired")]
        public bool ManualReviewRequired { get; set; }

        [JsonPropertyName("modelUsed")]
        public string? ModelUsed { get; set; }

        [JsonPropertyName("rawAiResponse")]
        public string? RawAiResponse { get; set; }

        [JsonPropertyName("extractionMethod")]
        public string? ExtractionMethod { get; set; }

        [JsonPropertyName("extractionFailureReason")]
        public string? ExtractionFailureReason { get; set; }

        [JsonPropertyName("documentMetadata")]
        public object? DocumentMetadata { get; set; }

        [JsonIgnore]
        public bool EvaluationLocked { get; set; }
    }
}



