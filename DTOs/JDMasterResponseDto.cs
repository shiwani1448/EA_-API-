namespace hrms_api.DTOs;

public class JDMasterResponseDto
{
    public int Id { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? JobDescription { get; set; }
    public string? SkillsRequired { get; set; }
    public string? Qualification { get; set; }
    public string? Experience { get; set; }
    public string? DocumentPdfPath { get; set; }
    public string? DocumentPdfFileName { get; set; }
    public string? DocumentPdfContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
