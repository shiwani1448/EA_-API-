namespace Jarvis5.Dtos.EaFms;

/// <summary>
/// Read-only projection of the existing PriorityLevel master, for frontend dropdowns
/// (e.g. Delegation's Priority field). Delegation.Priority — and every other consumer of
/// this catalog — stores the canonical Name string, never this Id.
/// </summary>
public sealed class PriorityLevelDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}
