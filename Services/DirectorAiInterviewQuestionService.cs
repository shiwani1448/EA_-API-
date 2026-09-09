using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.EntityFrameworkCore;

namespace hrms_api.Services;

public class DirectorAiInterviewQuestionService : IDirectorAiInterviewQuestionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly AppDbContext _db;
    private readonly IClaudeService _claude;
    private readonly IDirectorRoundInsightService _insightService;
    private readonly ILogger<DirectorAiInterviewQuestionService> _logger;

    public DirectorAiInterviewQuestionService(
        AppDbContext db,
        IClaudeService claude,
        IDirectorRoundInsightService insightService,
        ILogger<DirectorAiInterviewQuestionService> logger)
    {
        _db = db;
        _claude = claude;
        _insightService = insightService;
        _logger = logger;
    }

    public async Task<(int StatusCode, DirectorAiInterviewQuestionsResponseDto Response)> GenerateAsync(
        GenerateDirectorAiInterviewQuestionsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureDirectorAiInterviewQuestionsTableAsync(cancellationToken);

        if (request.CandidateId <= 0)
            return BadRequest(request.CandidateId, request.InterviewRoundId, "CandidateId is required.");

        if (request.InterviewRoundId <= 0)
            return BadRequest(request.CandidateId, request.InterviewRoundId, "InterviewRoundId is required.");

        var insightResult = await _insightService.GetCandidateInsightAsync(
            request.CandidateId,
            request.InterviewRoundId,
            cancellationToken);

        if (insightResult.StatusCode != StatusCodes.Status200OK || insightResult.Response.Data is null)
            return Status(request.CandidateId, request.InterviewRoundId, insightResult.StatusCode, insightResult.Response.Message);

        var existing = await LoadExistingQuestionsAsync(request.CandidateId, request.InterviewRoundId, cancellationToken);
        if (existing.Count > 0)
            return (StatusCodes.Status200OK, MapResponse(existing, "Director AI interview questions already generated.", insightResult.Response.Data));

        var data = insightResult.Response.Data;
        var prompt = BuildPrompt(data);
        string rawResponse;

        try
        {
            rawResponse = await _claude.GenerateAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI service failed while generating Director questions for candidate {CandidateId}", request.CandidateId);
            return Status(request.CandidateId, request.InterviewRoundId, StatusCodes.Status502BadGateway, "AI service failed.");
        }

        var aiResult = TryParseAiResponse(rawResponse);
        if (!HasValidQuestionCount(aiResult))
        {
            _logger.LogWarning("Invalid Director AI JSON for candidate {CandidateId}. Raw response: {Raw}", request.CandidateId, rawResponse);
            rawResponse = await RetryForValidJsonAsync(prompt, rawResponse, cancellationToken);
            aiResult = TryParseAiResponse(rawResponse);

            if (!HasValidQuestionCount(aiResult))
            {
                _logger.LogWarning("Director AI JSON retry failed for candidate {CandidateId}. Raw response: {Raw}", request.CandidateId, rawResponse);
                return BadRequest(request.CandidateId, request.InterviewRoundId, "AI returned invalid JSON.");
            }
        }

        var questions = aiResult!.Questions!;
        var technicalAssessmentScore = ResolveTechnicalAssessmentScore(data.TechnicalAssessmentInsight);
        var overallGapSummary = string.IsNullOrWhiteSpace(aiResult.OverallGapSummary)
            ? BuildFallbackGapSummary(data)
            : aiResult.OverallGapSummary.Trim();
        var now = DateTime.UtcNow;

        var rows = questions
            .OrderBy(q => q.QuestionNo)
            .Take(7)
            .Select((q, index) => new DirectorAiInterviewQuestion
            {
                CandidateId = request.CandidateId,
                InterviewRoundId = request.InterviewRoundId,
                ScreeningScore = data.ScreeningInsight.ScreeningScore,
                DiscProfile = data.DiscInsight.DiscProfile,
                DScore = data.DiscInsight.DScore,
                IScore = data.DiscInsight.IScore,
                SScore = data.DiscInsight.SScore,
                CScore = data.DiscInsight.CScore,
                HrRoundScore = data.HrRoundInsight.HrRoundScore,
                TechnicalAssessmentScore = technicalAssessmentScore,
                OverallGapSummary = overallGapSummary,
                QuestionNo = q.QuestionNo > 0 ? q.QuestionNo : index + 1,
                Question = q.Question?.Trim() ?? string.Empty,
                WhyDirectorShouldAskThis = q.WhyDirectorShouldAskThis?.Trim(),
                GapOrRiskArea = q.GapOrRiskArea?.Trim(),
                StrongAnswerSignals = SerializeList(q.StrongAnswerSignals),
                RedFlagSignals = SerializeList(q.RedFlagSignals),
                IsActive = true,
                CreatedAt = now
            })
            .ToList();

        if (rows.Any(q => string.IsNullOrWhiteSpace(q.Question)))
            return BadRequest(request.CandidateId, request.InterviewRoundId, "AI returned invalid JSON.");

        _db.DirectorAiInterviewQuestions.AddRange(rows);
        await _db.SaveChangesAsync(cancellationToken);

        return (StatusCodes.Status200OK, MapResponse(rows, "Director AI interview questions generated successfully.", data));
    }

    public async Task<(int StatusCode, DirectorAiInterviewQuestionsResponseDto Response)> GetAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken = default)
    {
        await EnsureDirectorAiInterviewQuestionsTableAsync(cancellationToken);

        var questions = await LoadExistingQuestionsAsync(candidateId, interviewRoundId, cancellationToken);
        if (questions.Count == 0)
            return NotFound(candidateId, interviewRoundId, "Director AI interview questions not found.");

        var insightResult = await _insightService.GetCandidateInsightAsync(candidateId, interviewRoundId, cancellationToken);
        var insightData = insightResult.StatusCode == StatusCodes.Status200OK
            ? insightResult.Response.Data
            : null;

        return (StatusCodes.Status200OK, MapResponse(questions, "Director AI interview questions fetched successfully.", insightData));
    }

    private async Task<List<DirectorAiInterviewQuestion>> LoadExistingQuestionsAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken)
    {
        return await _db.DirectorAiInterviewQuestions
            .Where(q => q.CandidateId == candidateId
                && q.InterviewRoundId == interviewRoundId
                && q.IsActive)
            .OrderBy(q => q.QuestionNo)
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureDirectorAiInterviewQuestionsTableAsync(CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS public."DirectorAiInterviewQuestions" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "CandidateId" integer NOT NULL,
                "InterviewRoundId" integer NOT NULL,
                "ScreeningScore" numeric(5,2) NULL,
                "DiscProfile" text NULL,
                "DScore" numeric(5,2) NULL,
                "IScore" numeric(5,2) NULL,
                "SScore" numeric(5,2) NULL,
                "CScore" numeric(5,2) NULL,
                "HrRoundScore" numeric(5,2) NULL,
                "TechnicalAssessmentScore" numeric(5,2) NULL,
                "OverallGapSummary" text NULL,
                "QuestionNo" integer NOT NULL,
                "Question" text NOT NULL,
                "WhyDirectorShouldAskThis" text NULL,
                "GapOrRiskArea" text NULL,
                "StrongAnswerSignals" text NULL,
                "RedFlagSignals" text NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_DirectorAiInterviewQuestions" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_DirectorAiInterviewQuestions_CandidateId"
                ON public."DirectorAiInterviewQuestions" ("CandidateId");

            CREATE INDEX IF NOT EXISTS "IX_DirectorAiInterviewQuestions_InterviewRoundId"
                ON public."DirectorAiInterviewQuestions" ("InterviewRoundId");

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DirectorAiInterviewQuestions_CandidateId_InterviewRoundId_QuestionNo"
                ON public."DirectorAiInterviewQuestions" ("CandidateId", "InterviewRoundId", "QuestionNo");
            """, cancellationToken);
    }

    private static string BuildPrompt(DirectorRoundCandidateInsightDataDto data)
    {
        var candidate = data.Candidate;
        var screening = data.ScreeningInsight;
        var disc = data.DiscInsight;
        var hr = data.HrRoundInsight;
        var technical = data.TechnicalAssessmentInsight;
        var technicalAssessmentScore = ResolveTechnicalAssessmentScore(technical);
        var previousQuestions = ExtractPreviousQuestionText(hr.QuestionWiseHrPerformance)
            .Concat(ExtractPreviousQuestionText(technical.QuestionWiseTechnicalPerformance))
            .Distinct()
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("You are a senior hiring director with 20 years of international recruitment and leadership hiring experience.");
        sb.AppendLine();
        sb.AppendLine("Generate up to 7 Director Round final validation questions for this candidate.");
        sb.AppendLine("Do not force 7 questions if that creates repeated, overlapping, or same-type questions. Fewer high-quality distinct questions are acceptable.");
        sb.AppendLine();
        sb.AppendLine("Candidate Data:");
        sb.AppendLine($"Department: {Value(candidate.Department)}");
        sb.AppendLine($"Designation: {Value(candidate.Designation)}");
        sb.AppendLine($"Level: {Value(candidate.Level)}");
        sb.AppendLine();
        sb.AppendLine("Screening:");
        sb.AppendLine($"Screening Score: {Value(screening.ScreeningScore)}");
        sb.AppendLine($"Screening Remarks: {Value(screening.ScreeningRemarks ?? screening.ResumeMatchSummary ?? screening.MissingReason)}");
        sb.AppendLine($"Screening Strengths: {Value(screening.ScreeningStrengths)}");
        sb.AppendLine($"Screening Weaknesses: {Value(screening.ScreeningWeaknesses)}");
        sb.AppendLine();
        sb.AppendLine("DISC:");
        sb.AppendLine($"DISC Profile: {Value(disc.DiscProfile)}");
        sb.AppendLine($"D Score: {Value(disc.DScore)}");
        sb.AppendLine($"I Score: {Value(disc.IScore)}");
        sb.AppendLine($"S Score: {Value(disc.SScore)}");
        sb.AppendLine($"C Score: {Value(disc.CScore)}");
        sb.AppendLine($"DISC Risk: {Value(disc.RiskBehavior ?? disc.MissingReason)}");
        sb.AppendLine();
        sb.AppendLine("HR Round:");
        sb.AppendLine($"HR Score: {Value(hr.HrRoundScore)}");
        sb.AppendLine($"HR Remarks: {Value(hr.HRRemarks ?? hr.MissingReason)}");
        sb.AppendLine($"HR Strengths: {Value(hr.HRStrengths)}");
        sb.AppendLine($"HR Concerns: {Value(hr.HRConcerns)}");
        sb.AppendLine();
        sb.AppendLine("Technical / Assessment:");
        sb.AppendLine($"Technical Assessment Score: {Value(technicalAssessmentScore)}");
        sb.AppendLine($"Technical Strengths: {Value(technical.StrongSkillAreas)}");
        sb.AppendLine($"Technical Weaknesses: {Value(technical.WeakSkillAreas)}");
        sb.AppendLine($"Interviewer Remarks: {Value(technical.InterviewerRemarks ?? technical.MissingReason)}");
        sb.AppendLine();
        sb.AppendLine("Known Previous HR or Technical Questions To Avoid:");
        sb.AppendLine(Value(previousQuestions));
        sb.AppendLine();
        sb.AppendLine("Available Data Gaps:");
        sb.AppendLine(BuildFallbackGapSummary(data));
        sb.AppendLine();
        sb.AppendLine("Goal:");
        sb.AppendLine("Director Round must avoid wrong hiring decisions by validating the candidate's biggest gaps, risks, maturity, ownership, stability, attitude, business fit, and long-term suitability.");
        sb.AppendLine();
        sb.AppendLine("Question Rules:");
        sb.AppendLine("1. Do not repeat HR Round questions.");
        sb.AppendLine("2. Do not repeat Technical Round questions.");
        sb.AppendLine("3. Do not ask random questions.");
        sb.AppendLine("4. Every question must be connected to an actual score, gap, DISC behavior, or risk.");
        sb.AppendLine("5. Questions should help Director decide Hire, Hold, Reject, or Move to Closing Round.");
        sb.AppendLine("6. Focus on risk validation, ownership, maturity, role seriousness, pressure handling, stability, and business fit.");
        sb.AppendLine("7. If technical score is high but HR score is weak, focus on attitude, communication, culture fit, and stability.");
        sb.AppendLine("8. If HR score is high but technical score is weak, focus on practical readiness and learning ability.");
        sb.AppendLine("9. If DISC is High D, validate teamwork, patience, listening, and aggression control.");
        sb.AppendLine("10. If DISC is High I, validate follow-up discipline, consistency, documentation, and seriousness.");
        sb.AppendLine("11. If DISC is High S, validate urgency, ownership, adaptability, and decision-making.");
        sb.AppendLine("12. If DISC is High C, validate flexibility, speed, pressure handling, and practical decision-making.");
        sb.AppendLine("13. Do not generate the same type of question twice. Each question must validate a different decision area such as ownership, maturity, stability, attitude, business fit, pressure handling, technical readiness, communication, or learning ability.");
        sb.AppendLine("14. If two questions would test the same risk in the same way, keep only the stronger question.");
        sb.AppendLine("15. Return only valid JSON.");
        sb.AppendLine();
        sb.AppendLine("Return JSON only:");
        sb.AppendLine("""
            {
              "overallGapSummary": "",
              "questions": [
                {
                  "questionNo": 1,
                  "question": "",
                  "whyDirectorShouldAskThis": "",
                  "gapOrRiskArea": "",
                  "strongAnswerSignals": [],
                  "redFlagSignals": []
                }
              ]
            }
            """);
        return sb.ToString();
    }

    private static List<string> ExtractPreviousQuestionText(object? value)
    {
        if (value is null)
            return new List<string>();

        try
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOptions));
            var questions = new List<string>();
            CollectQuestionText(document.RootElement, questions);
            return questions;
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void CollectQuestionText(JsonElement element, List<string> questions)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (Normalize(property.Name).Contains("question") && property.Value.ValueKind == JsonValueKind.String)
                {
                    var text = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        questions.Add(text);
                }

                CollectQuestionText(property.Value, questions);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectQuestionText(item, questions);
        }
    }

    private static string BuildFallbackGapSummary(DirectorRoundCandidateInsightDataDto data)
    {
        var gaps = new List<string>();

        if (data.ScreeningInsight.IsMissing)
            gaps.Add(data.ScreeningInsight.MissingReason ?? "Screening data missing.");
        else if (data.ScreeningInsight.ScreeningScore < 60)
            gaps.Add($"Low screening score: {data.ScreeningInsight.ScreeningScore}.");

        if (data.DiscInsight.IsMissing)
            gaps.Add(data.DiscInsight.MissingReason ?? "DISC data missing.");
        else if (!string.IsNullOrWhiteSpace(data.DiscInsight.RiskBehavior))
            gaps.Add(data.DiscInsight.RiskBehavior);

        if (data.HrRoundInsight.IsMissing)
            gaps.Add(data.HrRoundInsight.MissingReason ?? "HR Round data missing.");
        else if (data.HrRoundInsight.HrRoundScore < 60)
            gaps.Add($"Low HR Round score: {data.HrRoundInsight.HrRoundScore}.");

        if (data.TechnicalAssessmentInsight.IsMissing)
            gaps.Add(data.TechnicalAssessmentInsight.MissingReason ?? "Technical or Assessment data missing.");
        else if (ResolveTechnicalAssessmentScore(data.TechnicalAssessmentInsight) < 60)
            gaps.Add($"Low Technical/Assessment score: {ResolveTechnicalAssessmentScore(data.TechnicalAssessmentInsight)}.");

        if (data.HrRoundInsight.HrRoundScore.HasValue &&
            ResolveTechnicalAssessmentScore(data.TechnicalAssessmentInsight).HasValue &&
            Math.Abs(data.HrRoundInsight.HrRoundScore.Value - ResolveTechnicalAssessmentScore(data.TechnicalAssessmentInsight)!.Value) >= 25)
        {
            gaps.Add("Mismatch between HR and Technical/Assessment performance.");
        }

        gaps.AddRange(data.ScreeningInsight.ScreeningWeaknesses);
        gaps.AddRange(data.HrRoundInsight.HRConcerns);
        gaps.AddRange(data.TechnicalAssessmentInsight.WeakSkillAreas);

        return gaps.Count == 0
            ? "No major gap found in available data; Director should validate final ownership, maturity, stability, and business fit."
            : string.Join(" ", gaps.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct().Take(8));
    }

    private static decimal? ResolveTechnicalAssessmentScore(DirectorTechnicalAssessmentInsightDto technical)
    {
        var scores = new List<decimal>();
        if (technical.TechnicalScore.HasValue) scores.Add(technical.TechnicalScore.Value);
        if (technical.AssessmentScore.HasValue) scores.Add(technical.AssessmentScore.Value);
        return scores.Count == 0 ? null : Math.Round(scores.Average(), 2);
    }

    private static DirectorAiQuestionsAiResult? TryParseAiResponse(string rawResponse)
    {
        var json = ExtractJsonBlock(rawResponse);
        if (json is null)
            return null;

        var parsed = TryDeserialize(json);
        if (parsed is not null)
            return parsed;

        var repaired = System.Text.RegularExpressions.Regex.Replace(json, @",\s*([}\]])", "$1");
        return TryDeserialize(repaired);
    }

    private static bool HasValidQuestionCount(DirectorAiQuestionsAiResult? aiResult) =>
        aiResult?.Questions is { Count: >= 1 and <= 7 };

    private async Task<string> RetryForValidJsonAsync(
        string originalPrompt,
        string invalidResponse,
        CancellationToken cancellationToken)
    {
        var retryPrompt = new StringBuilder();
        retryPrompt.AppendLine(originalPrompt);
        retryPrompt.AppendLine();
        retryPrompt.AppendLine("Your previous response was invalid or incomplete JSON.");
        retryPrompt.AppendLine("Return ONLY one complete valid JSON object. No markdown. No explanation.");
        retryPrompt.AppendLine("Keep each field concise so the JSON is complete.");
        retryPrompt.AppendLine("The JSON must contain 1 to 7 items in questions.");
        retryPrompt.AppendLine("Do not repeat the same question type. Fewer distinct questions are better than repeated questions.");
        retryPrompt.AppendLine();
        retryPrompt.AppendLine("Previous invalid response:");
        retryPrompt.AppendLine(invalidResponse);

        return await _claude.GenerateAsync(retryPrompt.ToString(), cancellationToken);
    }

    private static DirectorAiQuestionsAiResult? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DirectorAiQuestionsAiResult>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractJsonBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = System.Text.RegularExpressions.Regex.Replace(text, @"```(?:json)?\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var start = text.IndexOf('{');
        if (start < 0)
            return null;

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

    private static DirectorAiInterviewQuestionsResponseDto MapResponse(
        List<DirectorAiInterviewQuestion> questions,
        string message,
        DirectorRoundCandidateInsightDataDto? data = null)
    {
        var first = questions.FirstOrDefault();
        return new DirectorAiInterviewQuestionsResponseDto
        {
            Success = true,
            Message = message,
            CandidateId = first?.CandidateId ?? 0,
            InterviewRoundId = first?.InterviewRoundId ?? 0,
            Candidate = data?.Candidate,
            CandidateDates = data?.CandidateDates,
            CandidateTimeline = data?.CandidateTimeline ?? new List<DirectorCandidateTimelineItemDto>(),
            Department = data?.Candidate.Department,
            Designation = data?.Candidate.Designation,
            Level = data?.Candidate.Level,
            ScreeningScore = first?.ScreeningScore,
            DiscProfile = first?.DiscProfile,
            DScore = first?.DScore,
            IScore = first?.IScore,
            SScore = first?.SScore,
            CScore = first?.CScore,
            HrRoundScore = first?.HrRoundScore,
            TechnicalAssessmentScore = first?.TechnicalAssessmentScore,
            ScoreBreakdown = data?.ScoreBreakdown ?? BuildStoredScoreBreakdown(first),
            OverallAnalysis = data?.OverallAnalysis,
            DirectorSummary = BuildDirectorSummary(first, data),
            OverallGapSummary = first?.OverallGapSummary,
            Questions = questions
                .OrderBy(q => q.QuestionNo)
                .Select(q => new DirectorAiInterviewQuestionItemDto
                {
                    Id = q.Id,
                    QuestionNo = q.QuestionNo,
                    Question = q.Question,
                    WhyDirectorShouldAskThis = q.WhyDirectorShouldAskThis,
                    GapOrRiskArea = q.GapOrRiskArea,
                    StrongAnswerSignals = DeserializeList(q.StrongAnswerSignals),
                    RedFlagSignals = DeserializeList(q.RedFlagSignals)
                })
                .ToList()
        };
    }

    private static DirectorScoreBreakdownDto? BuildStoredScoreBreakdown(DirectorAiInterviewQuestion? question)
    {
        if (question is null)
            return null;

        var components = new List<DirectorScoreComponentDto>
        {
            NewStoredScoreComponent("AI Screening", question.ScreeningScore, "DirectorAiInterviewQuestions.ScreeningScore"),
            NewStoredScoreComponent("HR Round", question.HrRoundScore, "DirectorAiInterviewQuestions.HrRoundScore"),
            NewStoredScoreComponent("Technical / Assessment", question.TechnicalAssessmentScore, "DirectorAiInterviewQuestions.TechnicalAssessmentScore")
        };

        var included = components.Where(c => c.Score.HasValue).ToList();
        var overall = included.Count == 0 ? (decimal?)null : Math.Round(included.Average(c => c.Score!.Value), 2);

        return new DirectorScoreBreakdownDto
        {
            OverallFitScore = overall,
            CalculationMethod = "Equal-weight average of available stored score components.",
            Calculation = included.Count == 0
                ? "No stored score components available."
                : $"({string.Join(" + ", included.Select(c => $"{c.Score:0.##}"))}) / {included.Count} = {overall:0.##}",
            Components = components
        };
    }

    private static DirectorScoreComponentDto NewStoredScoreComponent(string name, decimal? score, string source) => new()
    {
        Name = name,
        Score = score,
        OutOf = 100,
        IncludedInOverall = score.HasValue,
        Source = source,
        Explanation = score.HasValue ? "Stored score used for Director question generation." : "Not available for this candidate."
    };

    private static string BuildDirectorSummary(
        DirectorAiInterviewQuestion? question,
        DirectorRoundCandidateInsightDataDto? data)
    {
        if (data is null)
            return question?.OverallGapSummary ?? "Director summary is based on stored generated questions.";

        var candidate = data.Candidate;
        var score = data.ScoreBreakdown.OverallFitScore?.ToString("0.##", CultureInfo.InvariantCulture) ?? "not available";
        var recommendation = data.OverallAnalysis.FinalRecommendation;
        var risk = data.OverallAnalysis.RiskLevel;
        var strengths = data.OverallAnalysis.CandidateStrengthSummary.Take(3).ToList();
        var concerns = data.OverallAnalysis.CandidateWeaknessSummary.Take(3).ToList();

        var sb = new StringBuilder();
        sb.Append($"{candidate.CandidateName ?? "Candidate"} is being reviewed for {Value(candidate.Designation)}");
        if (!string.IsNullOrWhiteSpace(candidate.Department))
            sb.Append($" in {candidate.Department}");
        sb.Append($". Overall fit score is {score}/100 with {risk} risk and recommendation: {recommendation}.");

        if (strengths.Count > 0)
            sb.Append($" Key strengths: {string.Join("; ", strengths)}.");
        if (concerns.Count > 0)
            sb.Append($" Key concerns: {string.Join("; ", concerns)}.");

        sb.Append($" Director focus: {question?.OverallGapSummary ?? data.OverallAnalysis.HiringRiskReason ?? "validate final ownership, maturity, stability, and business fit."}");
        return sb.ToString();
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string? SerializeList(List<string>? values) =>
        values is { Count: > 0 } ? JsonSerializer.Serialize(values) : null;

    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "Not available" : value;

    private static string Value(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "Not available";

    private static string Value(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "Not available";

    private static string Value(List<string> values) => values.Count == 0 ? "Not available" : string.Join("; ", values);

    private static string Normalize(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, @"[^a-z0-9]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).ToLowerInvariant();

    private static (int StatusCode, DirectorAiInterviewQuestionsResponseDto Response) BadRequest(
        int candidateId,
        int interviewRoundId,
        string message) => Status(candidateId, interviewRoundId, StatusCodes.Status400BadRequest, message);

    private static (int StatusCode, DirectorAiInterviewQuestionsResponseDto Response) NotFound(
        int candidateId,
        int interviewRoundId,
        string message) => Status(candidateId, interviewRoundId, StatusCodes.Status404NotFound, message);

    private static (int StatusCode, DirectorAiInterviewQuestionsResponseDto Response) Status(
        int candidateId,
        int interviewRoundId,
        int statusCode,
        string message) =>
        (statusCode, new DirectorAiInterviewQuestionsResponseDto
        {
            Success = false,
            Message = message,
            CandidateId = candidateId,
            InterviewRoundId = interviewRoundId
        });

    private sealed class DirectorAiQuestionsAiResult
    {
        [JsonPropertyName("overallGapSummary")]
        public string? OverallGapSummary { get; set; }

        [JsonPropertyName("questions")]
        public List<DirectorAiQuestionAiItem>? Questions { get; set; }
    }

    private sealed class DirectorAiQuestionAiItem
    {
        [JsonPropertyName("questionNo")]
        public int QuestionNo { get; set; }

        [JsonPropertyName("question")]
        public string? Question { get; set; }

        [JsonPropertyName("whyDirectorShouldAskThis")]
        public string? WhyDirectorShouldAskThis { get; set; }

        [JsonPropertyName("gapOrRiskArea")]
        public string? GapOrRiskArea { get; set; }

        [JsonPropertyName("strongAnswerSignals")]
        public List<string>? StrongAnswerSignals { get; set; }

        [JsonPropertyName("redFlagSignals")]
        public List<string>? RedFlagSignals { get; set; }
    }
}

