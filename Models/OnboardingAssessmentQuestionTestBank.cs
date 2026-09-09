namespace hrms_api.Models;

public class OnboardingAssessmentQuestionTestBank
{
    public int QuestionId { get; set; }
    public string? Question { get; set; }
    public string? Option1 { get; set; }
    public string? Option2 { get; set; }
    public string? Option3 { get; set; }
    public string? Option4 { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CreatedBy { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdationDate { get; set; }
}
