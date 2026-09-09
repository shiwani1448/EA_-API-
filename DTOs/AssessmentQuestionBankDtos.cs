namespace hrms_api.DTOs;

public class AssessmentQuestionBankDto
{
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? Level { get; set; }
    public string? RoundName { get; set; }
    public int QuestionNo { get; set; }
    public string? QuestionType { get; set; }
    public string? SkillArea { get; set; }
    public string? Difficulty { get; set; }
    public string? Question { get; set; }
    public string? ExpectedAnswer { get; set; }
    public int MaxScore { get; set; }
    public decimal Weightage { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AssessmentQuestionBankResponseDto
{
    public int Id { get; set; }
    public string Department { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string RoundName { get; set; } = string.Empty;
    public int QuestionNo { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public string? SkillArea { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string? ExpectedAnswer { get; set; }
    public int MaxScore { get; set; }
    public decimal Weightage { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AssessmentQuestionBankFilterDto
{
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? Level { get; set; }
    public string? RoundName { get; set; }
}

public class AssessmentQuestionBankBulkUploadDto
{
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? Level { get; set; }
    public string? RoundName { get; set; }
    public List<AssessmentQuestionBankBulkQuestionDto> Questions { get; set; } = new();
}

public class AssessmentQuestionBankBulkQuestionDto
{
    public int QuestionNo { get; set; }
    public string? QuestionType { get; set; }
    public string? SkillArea { get; set; }
    public string? Difficulty { get; set; }
    public string? Question { get; set; }
    public string? ExpectedAnswer { get; set; }
    public int MaxScore { get; set; }
    public decimal Weightage { get; set; }
    public bool IsActive { get; set; } = true;
}
