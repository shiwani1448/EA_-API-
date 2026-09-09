namespace hrms_api.Models;

public class AssessmentQuestionBank
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
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
