namespace Jarvis5.Dtos;

/// <summary>Body for PATCH /api/request/{id}/overall-dates — partial update: only
/// fields present (non-null) in the request are changed, everything else keeps its
/// current value.</summary>
public class UpdateRequestOverallDatesDto
{
    public DateTime? OverallStartDate { get; set; }
    public DateTime? OverallEndDate { get; set; }
}
