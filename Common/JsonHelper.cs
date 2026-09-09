using System.Text.Json;

namespace Jarvis5.Common;

public static class JsonHelper
{
    public static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        return JsonSerializer.Deserialize<List<T>>(json, JsonDefaults.Options) ?? new List<T>();
    }

    public static T DeserializeObjectOrDefault<T>(string? json) where T : new()
    {
        if (string.IsNullOrWhiteSpace(json)) return new T();
        return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options) ?? new T();
    }

    public static object? ParseOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<JsonElement>(json, JsonDefaults.Options);
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonDefaults.Options);
}
