using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using hrms_api.Data;
using hrms_api.DTOs;
using hrms_api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace hrms_api.Services;

public class HrAiInterviewQuestionService : IHrAiInterviewQuestionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly AppDbContext _db;
    private readonly IClaudeService _claude;
    private readonly ILogger<HrAiInterviewQuestionService> _logger;

    public HrAiInterviewQuestionService(
        AppDbContext db,
        IClaudeService claude,
        ILogger<HrAiInterviewQuestionService> logger)
    {
        _db = db;
        _claude = claude;
        _logger = logger;
    }

    public async Task<(int StatusCode, HrAiInterviewQuestionsResponseDto Response)> GenerateAsync(
        GenerateHrAiInterviewQuestionsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureHrAiInterviewQuestionsTableAsync(cancellationToken);

        if (request.CandidateId <= 0)
            return BadRequest(request.CandidateId, request.InterviewRoundId, "CandidateId is required.");

        if (request.InterviewRoundId <= 0)
            return BadRequest(request.CandidateId, request.InterviewRoundId, "InterviewRoundId is required.");

        var existing = await LoadExistingQuestionsAsync(request.CandidateId, request.InterviewRoundId, cancellationToken);
        if (existing.Count > 0)
            return (StatusCodes.Status200OK, MapResponse(existing, "HR AI interview questions already generated."));

        var candidate = await _db.Candidates
            .FirstOrDefaultAsync(c => c.CandidateId == request.CandidateId && !c.IsDeleted, cancellationToken);

        if (candidate is null)
            return NotFound(request.CandidateId, request.InterviewRoundId, "Candidate not found.");

        var roundValidation = await ValidateHrRoundAsync(request.CandidateId, request.InterviewRoundId, cancellationToken);
        if (!roundValidation.Exists)
            return NotFound(request.CandidateId, request.InterviewRoundId, "Interview round not found.");

        if (!roundValidation.IsHrRound)
            return BadRequest(request.CandidateId, request.InterviewRoundId, "Interview round is not HR Round.");

        var role = await LoadCandidateRoleAsync(candidate, cancellationToken);
        var disc = await LoadDiscResultAsync(request.CandidateId, cancellationToken);
        if (disc is null)
            return BadRequest(request.CandidateId, request.InterviewRoundId,
                "DISC result is required before generating HR Round AI questions.");

        var prompt = BuildPrompt(role.Department, role.Designation, disc);
        string rawResponse;

        try
        {
            rawResponse = await _claude.GenerateAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI service failed while generating HR questions for candidate {CandidateId}", request.CandidateId);
            return Status(request.CandidateId, request.InterviewRoundId, StatusCodes.Status502BadGateway, "AI service failed.");
        }

        var aiResult = TryParseAiResponse(rawResponse);
        if (aiResult?.Questions is not { Count: 10 })
        {
            _logger.LogWarning("Invalid HR AI JSON for candidate {CandidateId}. Raw response: {Raw}", request.CandidateId, rawResponse);
            rawResponse = await RetryForValidJsonAsync(prompt, rawResponse, cancellationToken);
            aiResult = TryParseAiResponse(rawResponse);

            if (aiResult?.Questions is not { Count: 10 })
            {
                _logger.LogWarning("HR AI JSON retry failed for candidate {CandidateId}. Raw response: {Raw}", request.CandidateId, rawResponse);
                return BadRequest(request.CandidateId, request.InterviewRoundId, "AI returned invalid JSON.");
            }
        }

        var now = DateTime.UtcNow;
        var rows = aiResult.Questions
            .OrderBy(q => q.QuestionNo)
            .Take(10)
            .Select((q, index) => new HrAiInterviewQuestion
            {
                CandidateId = request.CandidateId,
                InterviewRoundId = request.InterviewRoundId,
                Department = role.Department,
                Designation = role.Designation,
                DiscProfile = disc.DiscProfile,
                DScore = disc.DScore,
                IScore = disc.IScore,
                SScore = disc.SScore,
                CScore = disc.CScore,
                QuestionNo = q.QuestionNo > 0 ? q.QuestionNo : index + 1,
                Question = q.Question?.Trim() ?? string.Empty,
                WhyAskThis = q.WhyAskThis?.Trim(),
                ScoringGuideline = q.ScoringGuideline?.Trim(),
                StrongAnswerSignals = SerializeList(q.StrongAnswerSignals),
                RedFlagSignals = SerializeList(q.RedFlagSignals),
                IsActive = true,
                CreatedAt = now
            })
            .ToList();

        if (rows.Any(q => string.IsNullOrWhiteSpace(q.Question)))
            return BadRequest(request.CandidateId, request.InterviewRoundId, "AI returned invalid JSON.");

        _db.HrAiInterviewQuestions.AddRange(rows);
        await _db.SaveChangesAsync(cancellationToken);

        return (StatusCodes.Status200OK, MapResponse(rows, "HR AI interview questions generated successfully."));
    }

    public async Task<(int StatusCode, HrAiInterviewQuestionsResponseDto Response)> GetAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken = default)
    {
        await EnsureHrAiInterviewQuestionsTableAsync(cancellationToken);

        var questions = await LoadExistingQuestionsAsync(candidateId, interviewRoundId, cancellationToken);
        if (questions.Count == 0)
            return NotFound(candidateId, interviewRoundId, "HR AI interview questions not found.");

        return (StatusCodes.Status200OK, MapResponse(questions, "HR AI interview questions fetched successfully."));
    }

    private async Task<List<HrAiInterviewQuestion>> LoadExistingQuestionsAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken)
    {
        return await _db.HrAiInterviewQuestions
            .Where(q => q.CandidateId == candidateId
                && q.InterviewRoundId == interviewRoundId
                && q.IsActive)
            .OrderBy(q => q.QuestionNo)
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureHrAiInterviewQuestionsTableAsync(CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS public."HrAiInterviewQuestions" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "CandidateId" integer NOT NULL,
                "InterviewRoundId" integer NOT NULL,
                "Department" text NULL,
                "Designation" text NULL,
                "DiscProfile" text NULL,
                "DScore" numeric(5,2) NULL,
                "IScore" numeric(5,2) NULL,
                "SScore" numeric(5,2) NULL,
                "CScore" numeric(5,2) NULL,
                "QuestionNo" integer NOT NULL,
                "Question" text NOT NULL,
                "WhyAskThis" text NULL,
                "ScoringGuideline" text NULL,
                "StrongAnswerSignals" text NULL,
                "RedFlagSignals" text NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NULL,
                CONSTRAINT "PK_HrAiInterviewQuestions" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_HrAiInterviewQuestions_CandidateId"
                ON public."HrAiInterviewQuestions" ("CandidateId");

            CREATE INDEX IF NOT EXISTS "IX_HrAiInterviewQuestions_InterviewRoundId"
                ON public."HrAiInterviewQuestions" ("InterviewRoundId");

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_HrAiInterviewQuestions_CandidateId_InterviewRoundId_QuestionNo"
                ON public."HrAiInterviewQuestions" ("CandidateId", "InterviewRoundId", "QuestionNo");
            """, cancellationToken);
    }

    private async Task<(string? Department, string? Designation)> LoadCandidateRoleAsync(
        Candidate candidate,
        CancellationToken cancellationToken)
    {
        var hiringRequest = await _db.HiringRequests
            .FirstOrDefaultAsync(h => h.RequestId == candidate.RequisitionId && !h.IsDeleted, cancellationToken);

        if (hiringRequest is null)
            return (null, null);

        if (!string.IsNullOrWhiteSpace(hiringRequest.Department) || !string.IsNullOrWhiteSpace(hiringRequest.Designation))
            return (hiringRequest.Department, hiringRequest.Designation);

        if (!hiringRequest.JDID.HasValue)
            return (hiringRequest.Department, hiringRequest.Designation);

        var jd = await _db.JDMasters
            .FirstOrDefaultAsync(j => j.Id == hiringRequest.JDID.Value && !j.IsDeleted, cancellationToken);

        return (jd?.Department, jd?.Designation);
    }

    private async Task<(bool Exists, bool IsHrRound)> ValidateHrRoundAsync(
        int candidateId,
        int interviewRoundId,
        CancellationToken cancellationToken)
    {
        var activity = await _db.CandidateActivities
            .FirstOrDefaultAsync(a => a.Id == interviewRoundId && a.CandidateId == candidateId, cancellationToken);

        if (activity is not null)
            return (true,
                IsHrRoundName(activity.Stage) &&
                activity.ActivityType.Equals("InterviewScheduled", StringComparison.OrdinalIgnoreCase));

        var table = await FindTableAsync(
            ["InterviewRounds", "InterviewRound", "CandidateInterviewRounds", "interview_rounds", "interview_round", "candidate_interview_rounds"],
            cancellationToken);
        if (table is null)
            return (false, false);

        var idColumn = FindColumn(table, ["Id", "InterviewRoundId", "RoundId"]);
        if (idColumn is null)
            return (false, false);

        var row = await QuerySingleRowAsync(table, idColumn, interviewRoundId, cancellationToken);
        if (row is null)
            return (false, false);

        var name = GetString(row, ["RoundName", "InterviewRoundName", "Name", "Round", "Stage", "ActivityType"]);
        return (true, IsHrRoundName(name));
    }

    private async Task<DiscResult?> LoadDiscResultAsync(int candidateId, CancellationToken cancellationToken)
    {
        var discActivity = await _db.CandidateActivities
            .Where(a => a.CandidateId == candidateId
                && a.EvaluationJson != null
                && a.ActivityType.ToLower() == "interviewcompleted"
                && a.Stage.ToLower() == "disc round")
            .OrderByDescending(a => a.ActionDate)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var discFromActivity = ParseDiscResult(discActivity?.EvaluationJson);
        if (discFromActivity is not null)
            return discFromActivity;

        var table = await FindTableAsync(
            ["CandidateDiscResults", "CandidateDISCResults", "CandidateDiscResult", "candidate_disc_results", "candidate_disc_result"],
            cancellationToken);
        if (table is null)
            return null;

        var candidateColumn = FindColumn(table, ["CandidateId"]);
        if (candidateColumn is null)
            return null;

        var row = await QueryLatestByCandidateAsync(table, candidateColumn, candidateId, cancellationToken);
        if (row is null)
            return null;

        return new DiscResult
        {
            DiscProfile = GetString(row, ["DiscProfile", "DISCProfile", "Profile", "ProfileName", "Result", "DiscResult"]),
            DScore = GetDecimal(row, ["DScore", "D", "DominanceScore"]),
            IScore = GetDecimal(row, ["IScore", "I", "InfluenceScore"]),
            SScore = GetDecimal(row, ["SScore", "S", "SteadinessScore"]),
            CScore = GetDecimal(row, ["CScore", "C", "ConscientiousnessScore", "ComplianceScore"])
        };
    }

    private static DiscResult? ParseDiscResult(JsonDocument? evaluationJson)
    {
        if (evaluationJson is null)
            return null;

        var root = evaluationJson.RootElement;
        var scoreElement = root;
        if (TryGetJsonProperty(root, "discScore", out var nestedScore) ||
            TryGetJsonProperty(root, "dISCScore", out nestedScore) ||
            TryGetJsonProperty(root, "DISCScore", out nestedScore))
        {
            scoreElement = nestedScore;
        }

        var result = new DiscResult
        {
            DiscProfile = GetJsonString(root, "discProfile", "dISCProfile", "DISCProfile"),
            DScore = GetJsonDecimal(scoreElement, "D", "dScore", "DScore"),
            IScore = GetJsonDecimal(scoreElement, "I", "iScore", "IScore"),
            SScore = GetJsonDecimal(scoreElement, "S", "sScore", "SScore"),
            CScore = GetJsonDecimal(scoreElement, "C", "cScore", "CScore")
        };

        return result.DiscProfile is not null ||
            result.DScore.HasValue ||
            result.IScore.HasValue ||
            result.SScore.HasValue ||
            result.CScore.HasValue
            ? result
            : null;
    }

    private static string? GetJsonString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetJsonProperty(element, name, out var property))
                return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        }

        return null;
    }

    private static decimal? GetJsonDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetJsonProperty(element, name, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
                return number;

            if (property.ValueKind == JsonValueKind.String &&
                decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return null;
    }

    private static bool TryGetJsonProperty(JsonElement element, string name, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out property))
            return true;

        var normalizedName = NormalizeIdentifier(name);
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var item in element.EnumerateObject())
            {
                if (NormalizeIdentifier(item.Name) == normalizedName)
                {
                    property = item.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private async Task<ExistingTable?> FindTableAsync(string[] tableNames, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND lower(table_name) = ANY(@names)
            ORDER BY array_position(@names, lower(table_name))
            LIMIT 1;
            """;
        command.Parameters.Add(new NpgsqlParameter<string[]>("names", tableNames.Select(n => n.ToLowerInvariant()).ToArray()));

        var tableName = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(tableName))
            return null;

        var columns = await LoadColumnsAsync(tableName, cancellationToken);
        return new ExistingTable(tableName, columns.ToDictionary(c => c.ToLowerInvariant(), c => c));
    }

    private async Task<List<string>> LoadColumnsAsync(string tableName, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = @tableName;
            """;
        command.Parameters.Add(new NpgsqlParameter<string>("tableName", tableName));

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(reader.GetString(0));

        return columns;
    }

    private async Task<Dictionary<string, object?>?> QuerySingleRowAsync(
        ExistingTable table,
        string idColumn,
        int id,
        CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""SELECT * FROM {Quote(table.Name)} WHERE {Quote(idColumn)} = @id LIMIT 1;""";
        command.Parameters.Add(new NpgsqlParameter<int>("id", id));

        return await ReadSingleRowAsync(command, cancellationToken);
    }

    private async Task<Dictionary<string, object?>?> QueryLatestByCandidateAsync(
        ExistingTable table,
        string candidateColumn,
        int candidateId,
        CancellationToken cancellationToken)
    {
        var orderColumn = FindColumn(table, ["CreatedAt", "UpdatedAt", "Id"]);
        var orderSql = orderColumn is null ? string.Empty : $" ORDER BY {Quote(orderColumn)} DESC";

        var connection = _db.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""SELECT * FROM {Quote(table.Name)} WHERE {Quote(candidateColumn)} = @candidateId{orderSql} LIMIT 1;""";
        command.Parameters.Add(new NpgsqlParameter<int>("candidateId", candidateId));

        return await ReadSingleRowAsync(command, cancellationToken);
    }

    private static async Task<Dictionary<string, object?>?> ReadSingleRowAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
            row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);

        return row;
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
    }

    private static string? FindColumn(ExistingTable table, string[] names)
    {
        foreach (var name in names)
        {
            if (table.ColumnsByLowerName.TryGetValue(name.ToLowerInvariant(), out var column))
                return column;
        }

        foreach (var column in table.ColumnsByLowerName.Values)
        {
            if (names.Any(name => NormalizeIdentifier(name) == NormalizeIdentifier(column)))
                return column;
        }

        return null;
    }

    private static string? GetString(Dictionary<string, object?> row, string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetValue(row, name, out var value) && value is not null)
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static decimal? GetDecimal(Dictionary<string, object?> row, string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetValue(row, name, out var value) || value is null)
                continue;

            if (value is decimal d)
                return d;

            if (decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return null;
    }

    private static bool TryGetValue(Dictionary<string, object?> row, string name, out object? value)
    {
        if (row.TryGetValue(name, out value))
            return true;

        var normalizedName = NormalizeIdentifier(name);
        foreach (var item in row)
        {
            if (NormalizeIdentifier(item.Key) == normalizedName)
            {
                value = item.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool IsHrRoundName(string? roundName)
    {
        if (string.IsNullOrWhiteSpace(roundName))
            return false;

        var normalized = Regex.Replace(roundName, @"[^a-z0-9]", "", RegexOptions.IgnoreCase).ToLowerInvariant();
        return normalized is "hrround" or "hr" or "humanresourcesround";
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    private static string NormalizeIdentifier(string identifier) =>
        Regex.Replace(identifier, @"[^a-z0-9]", "", RegexOptions.IgnoreCase).ToLowerInvariant();

    private static string BuildPrompt(string? department, string? designation, DiscResult disc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a world-class HR interview strategist with 20 years of recruitment experience.");
        sb.AppendLine();
        sb.AppendLine("Generate exactly 10 HR Round interview questions for the candidate below.");
        sb.AppendLine();
        sb.AppendLine("Candidate Details:");
        sb.AppendLine($"Department: {department ?? "Not available"}");
        sb.AppendLine($"Designation: {designation ?? "Not available"}");
        sb.AppendLine($"DISC Profile: {disc.DiscProfile ?? "Not available"}");
        sb.AppendLine($"D Score: {disc.DScore?.ToString(CultureInfo.InvariantCulture) ?? "Not available"}");
        sb.AppendLine($"I Score: {disc.IScore?.ToString(CultureInfo.InvariantCulture) ?? "Not available"}");
        sb.AppendLine($"S Score: {disc.SScore?.ToString(CultureInfo.InvariantCulture) ?? "Not available"}");
        sb.AppendLine($"C Score: {disc.CScore?.ToString(CultureInfo.InvariantCulture) ?? "Not available"}");
        sb.AppendLine();
        sb.AppendLine("Purpose:");
        sb.AppendLine("The HR Round should evaluate communication, confidence, attitude, culture fit, stability, ownership, learning ability, emotional maturity, teamwork, discipline, and long-term suitability.");
        sb.AppendLine();
        sb.AppendLine("Important Rules:");
        sb.AppendLine("1. Questions must be practical and HR-friendly.");
        sb.AppendLine("2. Do not create technical questions.");
        sb.AppendLine("3. Questions must be suitable for the department and designation.");
        sb.AppendLine("4. Questions must be influenced by the DISC profile and DISC score.");
        sb.AppendLine("5. Questions should help HR identify strengths, risks, and behavioral fit.");
        sb.AppendLine("6. Keep questions simple, professional, and directly askable.");
        sb.AppendLine("7. Return only valid JSON.");
        sb.AppendLine("8. Generate exactly 10 questions.");
        sb.AppendLine();
        sb.AppendLine("Return JSON only in this format:");
        sb.AppendLine("""
            {
              "questions": [
                {
                  "questionNo": 1,
                  "question": "",
                  "whyAskThis": "",
                  "scoringGuideline": "",
                  "strongAnswerSignals": [],
                  "redFlagSignals": []
                }
              ]
            }
            """);
        sb.AppendLine();
        sb.AppendLine("Scoring guideline format:");
        sb.AppendLine("Use simple HR scoring guidance from 1 to 5.");
        sb.AppendLine("1 = Poor answer");
        sb.AppendLine("2 = Weak answer");
        sb.AppendLine("3 = Average answer");
        sb.AppendLine("4 = Good answer");
        sb.AppendLine("5 = Excellent answer");
        return sb.ToString();
    }

    private static HrAiQuestionsAiResult? TryParseAiResponse(string rawResponse)
    {
        var json = ExtractJsonBlock(rawResponse);
        if (json is null)
            return null;

        var parsed = TryDeserialize(json);
        if (parsed is not null)
            return parsed;

        var repaired = Regex.Replace(json, @",\s*([}\]])", "$1");
        return TryDeserialize(repaired);
    }

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
        retryPrompt.AppendLine("The JSON must contain exactly 10 items in questions.");
        retryPrompt.AppendLine();
        retryPrompt.AppendLine("Previous invalid response:");
        retryPrompt.AppendLine(invalidResponse);

        return await _claude.GenerateAsync(retryPrompt.ToString(), cancellationToken);
    }

    private static HrAiQuestionsAiResult? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<HrAiQuestionsAiResult>(json, JsonOptions);
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

        text = Regex.Replace(text, @"```(?:json)?\s*", "", RegexOptions.IgnoreCase);
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

    private static HrAiInterviewQuestionsResponseDto MapResponse(
        List<HrAiInterviewQuestion> questions,
        string message)
    {
        var first = questions.FirstOrDefault();
        return new HrAiInterviewQuestionsResponseDto
        {
            Success = true,
            Message = message,
            CandidateId = first?.CandidateId ?? 0,
            InterviewRoundId = first?.InterviewRoundId ?? 0,
            Department = first?.Department,
            Designation = first?.Designation,
            DiscProfile = first?.DiscProfile,
            Questions = questions
                .OrderBy(q => q.QuestionNo)
                .Select(q => new HrAiInterviewQuestionItemDto
                {
                    Id = q.Id,
                    QuestionNo = q.QuestionNo,
                    Question = q.Question,
                    WhyAskThis = q.WhyAskThis,
                    ScoringGuideline = q.ScoringGuideline,
                    StrongAnswerSignals = DeserializeList(q.StrongAnswerSignals),
                    RedFlagSignals = DeserializeList(q.RedFlagSignals)
                })
                .ToList()
        };
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

    private static (int StatusCode, HrAiInterviewQuestionsResponseDto Response) BadRequest(
        int candidateId,
        int interviewRoundId,
        string message) => Status(candidateId, interviewRoundId, StatusCodes.Status400BadRequest, message);

    private static (int StatusCode, HrAiInterviewQuestionsResponseDto Response) NotFound(
        int candidateId,
        int interviewRoundId,
        string message) => Status(candidateId, interviewRoundId, StatusCodes.Status404NotFound, message);

    private static (int StatusCode, HrAiInterviewQuestionsResponseDto Response) Status(
        int candidateId,
        int interviewRoundId,
        int statusCode,
        string message) =>
        (statusCode, new HrAiInterviewQuestionsResponseDto
        {
            Success = false,
            Message = message,
            CandidateId = candidateId,
            InterviewRoundId = interviewRoundId
        });

    private sealed record ExistingTable(string Name, Dictionary<string, string> ColumnsByLowerName);

    private sealed class DiscResult
    {
        public string? DiscProfile { get; set; }
        public decimal? DScore { get; set; }
        public decimal? IScore { get; set; }
        public decimal? SScore { get; set; }
        public decimal? CScore { get; set; }
    }

    private sealed class HrAiQuestionsAiResult
    {
        [JsonPropertyName("questions")]
        public List<HrAiQuestionAiItem>? Questions { get; set; }
    }

    private sealed class HrAiQuestionAiItem
    {
        [JsonPropertyName("questionNo")]
        public int QuestionNo { get; set; }

        [JsonPropertyName("question")]
        public string? Question { get; set; }

        [JsonPropertyName("whyAskThis")]
        public string? WhyAskThis { get; set; }

        [JsonPropertyName("scoringGuideline")]
        public string? ScoringGuideline { get; set; }

        [JsonPropertyName("strongAnswerSignals")]
        public List<string>? StrongAnswerSignals { get; set; }

        [JsonPropertyName("redFlagSignals")]
        public List<string>? RedFlagSignals { get; set; }
    }
}

