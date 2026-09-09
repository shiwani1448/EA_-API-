using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jarvis5.Dtos;

public class RequestMetaDto
{
    /// <summary>
    /// Allows arbitrary properties to be passed through as open JSON.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
