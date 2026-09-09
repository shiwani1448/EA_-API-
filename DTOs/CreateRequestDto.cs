namespace Jarvis5.Dtos;

public class CreateRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public string RaisedBy { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ExpectedBenefit { get; set; } = string.Empty;
    public long? ParentRequestId { get; set; }
    public List<PainPointDto> PainPoints { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
    public RequestMetaDto? Meta { get; set; }
}
