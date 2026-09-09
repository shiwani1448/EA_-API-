namespace hrms_api.DTOs;

public class RejectCandidateDto
{
    public string Reason { get; set; } = string.Empty;
    public int? CreatedBy { get; set; }
}
