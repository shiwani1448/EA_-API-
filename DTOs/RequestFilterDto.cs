namespace Jarvis5.Dtos;

public class RequestFilterDto
{
    public string? Department { get; set; }
    public string? RaisedBy { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Search { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
