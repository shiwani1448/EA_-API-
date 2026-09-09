using System.Text.Json;
using System.Text.Json.Nodes;

namespace hrms_api.Services;

public sealed class OnboardingAiService : IOnboardingAiService
{
    private readonly IClaudeService _ai;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonElement EvaluationSchema = JsonDocument.Parse("""
        {
          "type":"object",
          "properties":{
            "result":{"type":"string","enum":["FIT","REVIEW","NOT_FIT"]},
            "overallScore":{"type":"number","minimum":0,"maximum":100},
            "confidence":{"type":"number","minimum":0,"maximum":1},
            "categoryScores":{
              "type":"object",
              "properties":{
                "technicalCapability":{"type":"number","minimum":0,"maximum":100},
                "practicalExecution":{"type":"number","minimum":0,"maximum":100},
                "roleUnderstanding":{"type":"number","minimum":0,"maximum":100},
                "problemSolving":{"type":"number","minimum":0,"maximum":100},
                "qualityOfWork":{"type":"number","minimum":0,"maximum":100},
                "consistency":{"type":"number","minimum":0,"maximum":100}
              },
              "required":["technicalCapability","practicalExecution","roleUnderstanding","problemSolving","qualityOfWork","consistency"],
              "additionalProperties":false
            },
            "strengths":{"type":"array","items":{"type":"string"}},
            "weaknesses":{"type":"array","items":{"type":"string"}},
            "dayWiseSummary":{
              "type":"array",
              "items":{
                "type":"object",
                "properties":{
                  "day":{"type":"integer","minimum":1},
                  "score":{"type":"number","minimum":0,"maximum":100},
                  "summary":{"type":"string"}
                },
                "required":["day","score","summary"],
                "additionalProperties":false
              }
            },
            "overallSummary":{"type":"string"},
            "recommendationForHR":{"type":"string"},
            "evidenceSummary":{"type":"object"},
            "evaluatedAt":{"type":"string"}
          },
          "required":["result","overallScore","confidence","categoryScores","strengths","weaknesses","dayWiseSummary","overallSummary","recommendationForHR","evidenceSummary","evaluatedAt"],
          "additionalProperties":false
        }
        """).RootElement.Clone();

    public OnboardingAiService(IClaudeService ai) => _ai = ai;

    public async Task<JsonDocument> GeneratePlanAsync(object input, int durationDays, CancellationToken cancellationToken = default)
    {
        var prompt = $$"""
            You generate a PRACTICAL TASK-ONLY WORK PLAN for a newly onboarded candidate. Create exactly {{durationDays}} sequential days.
            Every task must directly test the candidate's applied knowledge and practical ability for the
            exact designation in the input. Match difficulty primarily to designation, job description, required skills,
            responsibilities and experience; salary is secondary calibration.
            Do not ask about HR policy, attendance, leave, working hours, code of conduct, induction, payroll, company
            rules, or generic administration unless the candidate's actual designation is an HR role and that topic is
            an explicit job responsibility. Do not invent company-specific policies or ask the candidate to review them.
            100 percent of the plan must be designation-specific practical work. Generate NO questions, quizzes, MCQs,
            interviews, knowledge checks, or question/answer sections. Do not include a questions property anywhere.
            Example: for Sales Coordinator, focus on lead qualification, CRM/pipeline updates, follow-up scheduling,
            quotation/order coordination, handling customer and sales-team communication, sales reporting, target
            tracking, and resolving sales-process issues—not HR or employee-policy topics.
            Each day must total approximately the dailyWorkingHours in the input and contain 1 to 3 meaningful tasks,
            never trivial 15-30 minute work. Each day must contain day, title, objective, totalEstimatedHours, tasks.
            Each task must contain taskId, title, description, instructions, estimatedHours, required, submissionTypes,
            and evaluationCriteria. IDs must be unique and use D{day}T{number}.
            Allowed submissionTypes only: TEXT, IMAGE, PDF, EXCEL, VIDEO, FILE, MULTI_FILE.
            Return strict JSON only (no markdown) with durationDays, dailyTargetHours, and days.
            Input: {{JsonSerializer.Serialize(input, JsonOptions)}}
            """;
        var raw = await _ai.GenerateJsonAsync(prompt, cancellationToken);
        JsonDocument? aiPlan = null;
        try { aiPlan = ParseJson(raw); } catch (ArgumentException) { }
        var normalized = NormalizePlan(aiPlan, JsonSerializer.SerializeToNode(input, JsonOptions)!.AsObject(), durationDays);
        return ParseAndValidatePlan(normalized.RootElement.GetRawText(), durationDays);
    }

    public async Task<JsonDocument> EvaluateFinalAsync(object input, int durationDays, CancellationToken cancellationToken = default)
    {
        var prompt = $$"""
            You are the final practical-work onboarding evaluator. Evaluate all submitted tasks once against the actual
            role, job description, required skills, experience level, each task's objective, instructions, evaluation
            criteria, text submission, and processed file evidence. Do not evaluate grammar or invent evidence.
            When evidence is unavailable/unsupported, set manualReviewRequired and reduce confidence rather than claiming
            it was reviewed. Return strict JSON only.
            Required schema: {"result":"FIT|REVIEW|NOT_FIT","overallScore":0,"confidence":0.0,
            "categoryScores":{"technicalCapability":0,"practicalExecution":0,"roleUnderstanding":0,"problemSolving":0,
            "qualityOfWork":0,"consistency":0},"strengths":[],"weaknesses":[],
            "dayWiseSummary":[{"day":1,"score":0,"summary":""}],"overallSummary":"",
            "recommendationForHR":"","evidenceSummary":{"tasksEvaluated":0,"filesReviewed":0,"manualReviewRequired":false},"evaluatedAt":"ISO-8601 UTC"}.
            Include exactly {{durationDays}} dayWiseSummary entries. Input: {{JsonSerializer.Serialize(input, JsonOptions)}}
            """;
        return ParseAndValidateEvaluation(await _ai.GenerateJsonAsync(prompt, EvaluationSchema, cancellationToken), durationDays);
    }

    internal static JsonDocument ParseAndValidatePlan(string raw, int durationDays)
    {
        var doc = ParseJson(raw);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("days", out var days) ||
            days.ValueKind != JsonValueKind.Array || days.GetArrayLength() != durationDays)
            throw new ArgumentException($"AI plan must contain exactly {durationDays} days.");
        if (!root.TryGetProperty("durationDays", out _) || !root.TryGetProperty("dailyTargetHours", out _))
            throw new ArgumentException("AI task plan is missing durationDays or dailyTargetHours.");
        var expected = 1;
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!root.GetProperty("dailyTargetHours").TryGetDouble(out var dailyTargetHours) || dailyTargetHours is < 1 or > 12)
            throw new ArgumentException("AI task plan has an invalid dailyTargetHours value.");
        foreach (var day in days.EnumerateArray())
        {
            if (!day.TryGetProperty("day", out var number) || number.GetInt32() != expected++)
                throw new ArgumentException("AI plan days must be sequential starting at day 1.");
            RequiredString(day, "title"); RequiredString(day, "objective");
            if (day.TryGetProperty("questions", out _)) throw new ArgumentException("AI task plan must not contain questions.");
            ValidateScore(day, "totalEstimatedHours", 0.5, 12);
            ValidateItems(day, "tasks", "taskId", ids, requireAtLeastOne: true);
            var taskItems = day.GetProperty("tasks");
            if (taskItems.GetArrayLength() > 3) throw new ArgumentException("AI task plan may contain at most three tasks per day.");
            double taskHours = 0;
            foreach (var task in taskItems.EnumerateArray())
            {
                RequiredString(task, "title"); RequiredString(task, "description"); RequiredString(task, "instructions");
                ValidateScore(task, "estimatedHours", 0.5, 12);
                taskHours += task.GetProperty("estimatedHours").GetDouble();
                if (!task.TryGetProperty("required", out var required) || required.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new ArgumentException("AI task is missing required.");
                var types = RequiredArray(task, "submissionTypes");
                if (types.GetArrayLength() == 0 || types.EnumerateArray().Any(t => t.ValueKind != JsonValueKind.String ||
                    t.GetString() is not ("TEXT" or "IMAGE" or "PDF" or "EXCEL" or "VIDEO" or "FILE" or "MULTI_FILE")))
                    throw new ArgumentException("AI task contains an unsupported submission type.");
                if (RequiredArray(task, "evaluationCriteria").GetArrayLength() == 0)
                    throw new ArgumentException("AI task is missing evaluation criteria.");
            }
            if (Math.Abs(taskHours - dailyTargetHours) > 1)
                throw new ArgumentException("AI task hours must approximately match the daily working-hours target.");
        }
        return doc;
    }

    private static JsonDocument NormalizePlan(JsonDocument? source, JsonObject input, int durationDays)
    {
        var sourceRoot = source is null || source.RootElement.ValueKind != JsonValueKind.Object
            ? new JsonObject() : JsonNode.Parse(source.RootElement.GetRawText())!.AsObject();
        var designation = NodeString(FindValue(input, "designation")) ?? "the assigned role";
        var jobDescription = NodeString(FindValue(input, "jobDescription", "responsibilities")) ?? $"practical responsibilities of {designation}";
        var skills = NodeString(FindValue(input, "requiredSkills")) ?? $"core {designation} skills";
        var targetHours = Math.Clamp(NodeNumber(FindValue(input, "dailyWorkingHours")) ?? 8, 1, 12);
        var sourceDays = sourceRoot["days"] as JsonArray;
        var normalizedDays = new JsonArray();
        var allowedTypes = new HashSet<string>(["TEXT","IMAGE","PDF","EXCEL","VIDEO","FILE","MULTI_FILE"], StringComparer.OrdinalIgnoreCase);

        for (var dayNumber = 1; dayNumber <= durationDays; dayNumber++)
        {
            var suppliedDay = sourceDays?.OfType<JsonObject>().FirstOrDefault(d =>
                (int?)(NodeNumber(FindValue(d, "day"))) == dayNumber)
                ?? sourceDays?.OfType<JsonObject>().ElementAtOrDefault(dayNumber - 1);
            var sourceTasks = suppliedDay?["tasks"] as JsonArray;
            var normalizedTasks = new JsonArray();
            if (sourceTasks is not null)
            {
                var taskNumber = 1;
                foreach (var suppliedTask in sourceTasks.OfType<JsonObject>().Take(3))
                {
                    var title = NodeString(FindValue(suppliedTask, "title"));
                    var description = NodeString(FindValue(suppliedTask, "description"));
                    var instructions = NodeString(FindValue(suppliedTask, "instructions", "instruction"));
                    var requestedTypes = FindValue(suppliedTask, "submissionTypes", "submissionType") is JsonArray typeArray
                        ? typeArray.Select(NodeString).Where(x => x is not null && allowedTypes.Contains(x)).Select(x => x!.ToUpperInvariant()).Distinct().ToArray()
                        : new[] { NodeString(FindValue(suppliedTask, "submissionType"))?.ToUpperInvariant() }.Where(x => x is not null && allowedTypes.Contains(x)).ToArray()!;
                    var criteriaNode = FindValue(suppliedTask, "evaluationCriteria", "criteria") as JsonArray;
                    var criteria = criteriaNode?.Select(NodeString).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];
                    normalizedTasks.Add(new JsonObject
                    {
                        ["taskId"] = $"D{dayNumber}T{taskNumber++}",
                        ["title"] = string.IsNullOrWhiteSpace(title) ? $"{designation} Practical Assignment" : title,
                        ["description"] = string.IsNullOrWhiteSpace(description) ? $"Complete meaningful practical work for {designation}." : description,
                        ["instructions"] = string.IsNullOrWhiteSpace(instructions) ? $"Apply {skills} to produce a reviewable deliverable aligned with: {Trim(jobDescription, 600)}" : instructions,
                        ["estimatedHours"] = BoundedNumber(FindValue(suppliedTask, "estimatedHours", "hours"), .5, 12, targetHours),
                        ["required"] = true,
                        ["submissionTypes"] = new JsonArray((requestedTypes.Length == 0 ? ["TEXT", "PDF", "FILE"] : requestedTypes).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
                        ["evaluationCriteria"] = new JsonArray((criteria.Length == 0 ? ["Role relevance", "Accuracy", "Completeness", "Practical application"] : criteria).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray())
                    });
                }
            }
            if (normalizedTasks.Count == 0)
                normalizedTasks.Add(CreateFallbackTask(dayNumber, designation, jobDescription, skills, targetHours));

            var eachHours = targetHours / normalizedTasks.Count;
            foreach (var task in normalizedTasks.OfType<JsonObject>()) task["estimatedHours"] = eachHours;
            normalizedDays.Add(new JsonObject
            {
                ["day"] = dayNumber,
                ["title"] = NodeString(FindValueOrNull(suppliedDay, "title")) ?? $"{designation} Practical Work - Day {dayNumber}",
                ["objective"] = NodeString(FindValueOrNull(suppliedDay, "objective")) ?? $"Assess practical day-{dayNumber} readiness for {designation}.",
                ["totalEstimatedHours"] = targetHours,
                ["tasks"] = normalizedTasks
            });
        }
        var result = new JsonObject { ["durationDays"] = durationDays, ["dailyTargetHours"] = targetHours, ["days"] = normalizedDays };
        return JsonDocument.Parse(result.ToJsonString());
    }

    private static JsonObject CreateFallbackTask(int day, string role, string jd, string skills, double hours) => new()
    {
        ["taskId"] = $"D{day}T1", ["title"] = $"{role} Practical Assignment - Day {day}",
        ["description"] = $"Produce a substantive, reviewable work product that reflects the day-to-day responsibilities of {role}.",
        ["instructions"] = $"Use {skills}. Base the deliverable on these role requirements: {Trim(jd, 800)}. Document assumptions, approach, completed work, and a self-review.",
        ["estimatedHours"] = hours, ["required"] = true,
        ["submissionTypes"] = new JsonArray("TEXT", "PDF", "FILE"),
        ["evaluationCriteria"] = new JsonArray("Role relevance", "Technical accuracy", "Completeness", "Problem solving", "Quality of work")
    };

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];

    internal static JsonDocument ParseAndValidateEvaluation(string raw, int durationDays)
    {
        var doc = NormalizeEvaluation(ParseJson(raw), durationDays);
        var root = doc.RootElement;
        var result = RequiredString(root, "result");
        if (result is not ("FIT" or "REVIEW" or "NOT_FIT")) throw new ArgumentException("AI returned an invalid result.");
        ValidateScore(root, "overallScore", 0, 100);
        ValidateScore(root, "confidence", 0, 1);
        if (!root.TryGetProperty("categoryScores", out var categories) || categories.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("AI evaluation is missing categoryScores.");
        foreach (var name in new[] { "technicalCapability", "practicalExecution", "roleUnderstanding", "problemSolving", "qualityOfWork", "consistency" })
            ValidateScore(categories, name, 0, 100);
        RequiredArray(root, "strengths"); RequiredArray(root, "weaknesses");
        var summaries = RequiredArray(root, "dayWiseSummary");
        if (summaries.GetArrayLength() != durationDays) throw new ArgumentException("AI evaluation has an invalid day-wise summary.");
        var expectedDay = 1;
        foreach (var summary in summaries.EnumerateArray())
        {
            if (!summary.TryGetProperty("day", out var day) || day.GetInt32() != expectedDay++)
                throw new ArgumentException("AI evaluation day summaries must be sequential.");
            ValidateScore(summary, "score", 0, 100);
            RequiredString(summary, "summary");
        }
        RequiredString(root, "overallSummary"); RequiredString(root, "recommendationForHR");
        if (!DateTimeOffset.TryParse(RequiredString(root, "evaluatedAt"), out _)) throw new ArgumentException("AI evaluation has an invalid evaluatedAt value.");
        return doc;
    }

    private static JsonDocument NormalizeEvaluation(JsonDocument source, int durationDays)
    {
        var root = JsonNode.Parse(source.RootElement.GetRawText())?.AsObject()
            ?? throw new ArgumentException("AI evaluation must be a JSON object.");

        foreach (var name in new[]
        {
            "result", "overallScore", "confidence", "categoryScores", "strengths", "weaknesses",
            "dayWiseSummary", "overallSummary", "recommendationForHR", "evidenceSummary", "evaluatedAt"
        })
            CopyCanonical(root, name);

        var result = NodeString(root["result"])?.Trim().ToUpperInvariant();
        root["result"] = result is "FIT" or "NOT_FIT" or "REVIEW" ? result : "REVIEW";

        var overallScore = BoundedNumber(root["overallScore"], 0, 100, 0);
        root["overallScore"] = overallScore;
        var confidence = NodeNumber(root["confidence"]) ?? 0.5;
        if (confidence > 1 && confidence <= 100) confidence /= 100;
        root["confidence"] = Math.Clamp(confidence, 0, 1);

        var categories = root["categoryScores"] as JsonObject ?? new JsonObject();
        root["categoryScores"] = categories;
        foreach (var name in new[] { "technicalCapability", "practicalExecution", "roleUnderstanding", "problemSolving", "qualityOfWork", "consistency" })
        {
            CopyCanonical(categories, name);
            categories[name] = BoundedNumber(categories[name], 0, 100, overallScore);
        }

        root["strengths"] = NormalizeStringArray(root["strengths"]);
        root["weaknesses"] = NormalizeStringArray(root["weaknesses"]);

        var suppliedDays = root["dayWiseSummary"] as JsonArray;
        var normalizedDays = new JsonArray();
        for (var dayNumber = 1; dayNumber <= durationDays; dayNumber++)
        {
            var supplied = suppliedDays?.OfType<JsonObject>().FirstOrDefault(d =>
                (int?)NodeNumber(FindValue(d, "day")) == dayNumber);
            var score = BoundedNumber(FindValueOrNull(supplied, "score"), 0, 100, overallScore);
            var summary = NodeString(FindValueOrNull(supplied, "summary"));
            normalizedDays.Add(new JsonObject
            {
                ["day"] = dayNumber,
                ["score"] = score,
                ["summary"] = string.IsNullOrWhiteSpace(summary)
                    ? $"Day {dayNumber} performance contributed to an overall score of {overallScore:0.#}."
                    : summary
            });
        }
        root["dayWiseSummary"] = normalizedDays;

        var overallSummary = NodeString(root["overallSummary"])
            ?? NodeString(FindValue(root, "summary", "overallAssessment", "finalSummary"));
        if (string.IsNullOrWhiteSpace(overallSummary))
            overallSummary = string.Join(" ", normalizedDays.OfType<JsonObject>().Select(d => NodeString(d["summary"])));
        root["overallSummary"] = string.IsNullOrWhiteSpace(overallSummary)
            ? $"The completed onboarding assessment received an overall score of {overallScore:0.#}."
            : overallSummary;

        var recommendation = NodeString(root["recommendationForHR"])
            ?? NodeString(FindValue(root, "recommendation", "hrRecommendation", "recommendationForHr"));
        root["recommendationForHR"] = string.IsNullOrWhiteSpace(recommendation)
            ? root["result"]!.GetValue<string>() switch
            {
                "FIT" => "Candidate appears suitable; HR should review and approve if appropriate.",
                "NOT_FIT" => "Candidate shows role-readiness gaps; HR must review before making a decision.",
                _ => "Manual HR review is recommended before making the final onboarding decision."
            }
            : recommendation;

        var evidence = root["evidenceSummary"] as JsonObject ?? new JsonObject();
        evidence["tasksEvaluated"] = Math.Max(0, (int)(NodeNumber(FindValue(evidence, "tasksEvaluated")) ?? 0));
        evidence["filesReviewed"] = Math.Max(0, (int)(NodeNumber(FindValue(evidence, "filesReviewed")) ?? 0));
        if (FindValue(evidence, "manualReviewRequired") is not JsonNode manualNode || manualNode.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
            evidence["manualReviewRequired"] = false;
        root["evidenceSummary"] = evidence;

        // Server time is authoritative and avoids invalid/missing provider timestamps.
        root["evaluatedAt"] = DateTime.UtcNow.ToString("O");
        return JsonDocument.Parse(root.ToJsonString());
    }

    private static void CopyCanonical(JsonObject obj, string canonicalName)
    {
        if (obj.ContainsKey(canonicalName)) return;
        var value = FindValue(obj, canonicalName);
        if (value is not null) obj[canonicalName] = value.DeepClone();
    }

    private static JsonNode? FindValue(JsonObject obj, params string[] names)
    {
        var wanted = names.Select(NormalizeName).ToHashSet(StringComparer.Ordinal);
        return obj.FirstOrDefault(p => wanted.Contains(NormalizeName(p.Key))).Value;
    }

    private static JsonNode? FindValueOrNull(JsonObject? obj, params string[] names) =>
        obj is null ? null : FindValue(obj, names);

    private static string? NodeString(JsonNode? node)
    {
        if (node is null || node.GetValueKind() == JsonValueKind.Null) return null;
        if (node.GetValueKind() == JsonValueKind.String) return node.GetValue<string>();
        return node.ToJsonString().Trim('"');
    }

    private static double? NodeNumber(JsonNode? node)
    {
        if (node is null) return null;
        if (node.GetValueKind() == JsonValueKind.Number && node.AsValue().TryGetValue<double>(out var number)) return number;
        return double.TryParse(NodeString(node), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out number) ? number : null;
    }

    private static double BoundedNumber(JsonNode? node, double min, double max, double fallback) =>
        Math.Clamp(NodeNumber(node) ?? fallback, min, max);

    private static JsonArray NormalizeStringArray(JsonNode? node)
    {
        if (node is JsonArray array)
            return new JsonArray(array.Select(NodeString).Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => JsonValue.Create(s)).ToArray<JsonNode?>());
        var single = NodeString(node);
        return string.IsNullOrWhiteSpace(single) ? new JsonArray() : new JsonArray(single);
    }

    private static string NormalizeName(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool IsBlank(JsonNode? value) =>
        value is null || value.GetValueKind() == JsonValueKind.Null ||
        (value.GetValueKind() == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetValue<string>()));

    private static JsonDocument ParseJson(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLine = text.IndexOf('\n'); var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLine >= 0 && lastFence > firstLine) text = text[(firstLine + 1)..lastFence].Trim();
        }
        try { return JsonDocument.Parse(text); }
        catch (JsonException) when (TryExtractJsonObject(text, out var json))
        {
            try { return JsonDocument.Parse(json); }
            catch (JsonException ex) { throw new ArgumentException("AI returned malformed JSON.", ex); }
        }
        catch (JsonException ex) { throw new ArgumentException("AI returned malformed JSON.", ex); }
    }

    private static bool TryExtractJsonObject(string text, out string json)
    {
        json = string.Empty;
        var start = text.IndexOf('{');
        if (start < 0) return false;
        var depth = 0; var inString = false; var escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var ch = text[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (ch == '\\') escaped = true;
                else if (ch == '"') inString = false;
                continue;
            }
            if (ch == '"') inString = true;
            else if (ch == '{') depth++;
            else if (ch == '}' && --depth == 0) { json = text[start..(i + 1)]; return true; }
        }
        return false;
    }

    private static void ValidateItems(JsonElement day, string property, string idProperty, HashSet<string> ids, bool requireAtLeastOne)
    {
        if (!day.TryGetProperty(property, out var items) || items.ValueKind != JsonValueKind.Array || (requireAtLeastOne && items.GetArrayLength() == 0))
            throw new ArgumentException($"Every AI plan day must contain valid {property}.");
        foreach (var item in items.EnumerateArray())
            if (!ids.Add(RequiredString(item, idProperty))) throw new ArgumentException("AI plan contains duplicate item IDs.");
    }

    private static string RequiredString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()! : throw new ArgumentException($"AI response is missing {name}.");
    private static JsonElement RequiredArray(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value : throw new ArgumentException($"AI response is missing {name}.");
    private static void ValidateScore(JsonElement root, string name, double min, double max)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetDouble(out var score) || score < min || score > max)
            throw new ArgumentException($"AI response has an invalid {name}.");
    }
}

