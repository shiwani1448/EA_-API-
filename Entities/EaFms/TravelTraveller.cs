namespace Jarvis5.Entities.EaFms;

/// <summary>
/// One traveller row of a TravelRequest. Name, employee/person id, department and contact
/// information belong to the same person and are stored together on one row, so the
/// association between the four values can never be lost.
/// </summary>
public class TravelTraveller
{
    public long Id { get; set; }
    public long TravelRequestId { get; set; }
    public TravelRequest TravelRequest { get; set; } = null!;

    /// <summary>Position of this traveller in the submitted list (0-based); preserves row order.</summary>
    public int SortOrder { get; set; }

    public string? TravellerName { get; set; }
    // User/employee identity is owned by the separate HRMS context; no cross-context FK is safe.
    public string? EmployeePersonId { get; set; }
    public string? Department { get; set; }
    public string? ContactInformation { get; set; }
}
