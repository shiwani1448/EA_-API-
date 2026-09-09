using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jarvis5.Common;

/// <summary>Every DateTime in this project is UTC wall-clock time (see Clock.UtcNow),
/// but Kind is forced to Unspecified before it reaches the DB because Npgsql rejects
/// Kind=Utc against "timestamp without time zone" columns (see Clock.cs). Left alone,
/// System.Text.Json would then serialize it without a timezone designator, and clients
/// could misread it as local time. This converter restores the "Z" suffix on the way out
/// without touching how the value is stored.</summary>
public class DateTimeUtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

public class NullableDateTimeUtcConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
        else
            writer.WriteNullValue();
    }
}
