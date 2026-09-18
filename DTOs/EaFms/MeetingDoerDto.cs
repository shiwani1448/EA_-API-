using System.Text.Json.Serialization;
namespace Jarvis5.Dtos.EaFms;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class MeetingDoerDto
{
    // Frontend-owned form fields, not backend-mandatory. Nullable so ASP.NET Core's
    // implicit-required validation for non-nullable reference types (from [ApiController])
    // does not silently re-impose the requiredness FluentValidation deliberately omits.
    public string? DoerId { get; set; }
    public string? DoerName { get; set; }
}
