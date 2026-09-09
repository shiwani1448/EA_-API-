namespace hrms_api.DTOs;

public class OfferCandidateDto
{
    public decimal OfferedCtc { get; set; }
    public DateTime? JoiningDate { get; set; }
    public string? Remarks { get; set; }
    public int? CreatedBy { get; set; }
}
