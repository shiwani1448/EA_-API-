using System.Text.Json;

namespace Jarvis5.Common;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new DateTimeUtcConverter());
        options.Converters.Add(new NullableDateTimeUtcConverter());
        return options;
    }
}
