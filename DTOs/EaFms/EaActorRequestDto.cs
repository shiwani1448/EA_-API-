namespace Jarvis5.Dtos.EaFms;

/// <summary>Frontend-supplied actor snapshot for operations that have no other request body (e.g. deactivate).</summary>
public sealed class EaActorRequestDto
{
    public string? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
}
