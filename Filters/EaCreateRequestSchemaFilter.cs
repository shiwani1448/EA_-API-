using System.Text.Json.Nodes;
using Jarvis5.Dtos.EaFms;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Jarvis5.Filters;

// Limit these examples to EA creation contracts; other Swagger schemas are unchanged.
public class EaCreateRequestSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schemaDefinition, SchemaFilterContext context)
    {
        if (schemaDefinition is not OpenApiSchema schema) return;
        if (context.Type == typeof(CreateTravelRequestDto) || context.Type == typeof(ApprovalRequestDto))
        {
            // Neutral example only (Swagger display); no request-time defaults or requiredness are changed.
            schema.Example = NeutralExample(context.Type);
            return;
        }
        if (context.Type != typeof(CreateFollowupRequestDto) && context.Type != typeof(CreateMeetingRequestDto))
            return;

        if (schema.Properties != null)
        {
            foreach (var property in schema.Properties)
            {
                var clrProperty = context.Type.GetProperties().FirstOrDefault(p =>
                    string.Equals(p.Name, property.Key, StringComparison.OrdinalIgnoreCase));
                var numericType = clrProperty == null ? null : Nullable.GetUnderlyingType(clrProperty.PropertyType);
                if ((numericType != typeof(int) && numericType != typeof(long)) || property.Value is not OpenApiSchema field)
                    continue;

                field.Type = JsonSchemaType.Integer | JsonSchemaType.Null;
                field.Minimum = "1";
                field.Description = clrProperty!.Name == nameof(CreateFollowupRequestDto.SequenceNumber)
                    ? "Optional parent sequence number, not a follow-up cycle number. Omit or send null; no number needs to be manufactured. If supplied, must be greater than zero."
                    : "Optional reference. Omit or send null when unused. If supplied, use an existing positive ID; zero and negative IDs are invalid.";
                schema.Required?.Remove(property.Key);
            }
        }

        if (context.Type == typeof(CreateMeetingRequestDto))
        {
            schema.Example = new JsonObject
            {
                ["title"] = "Client Review", ["type"] = "Client", ["subtype"] = "Review",
                ["doers"] = new JsonArray(new JsonObject { ["doerId"] = "EMP001", ["doerName"] = "Person One" })
            };
        }
        else
        {
            schema.Description = "Create a standalone follow-up with all source fields null, or optionally link an existing intake, workflow, or business-module/record pair. "
                + "Subject and DueAt are required. Timestamps use UTC; next follow-up may precede the due date. Id and audit/completion fields are server-controlled and must not be supplied.";
            schema.Required ??= new HashSet<string>();
            schema.Required.Add("subject");
            schema.Required.Add("dueAt");
            schema.AdditionalPropertiesAllowed = false;
            if (schema.Properties != null && schema.Properties.TryGetValue("subject", out var subjectDefinition)
                && subjectDefinition is OpenApiSchema subject)
            {
                subject.Type = JsonSchemaType.String;
                subject.MinLength = 1;
                subject.MaxLength = 500;
                subject.Description = "Required nonblank subject, trimmed before storage; maximum 500 characters.";
            }
            // Neutral example built from the real DTO properties: no business-looking sample data.
            // Optional (nullable/reference) properties are null and channel flags are false. A non-nullable
            // DateTime (DueAt) is omitted because JSON null cannot bind to it; the backend treats an omitted
            // DueAt as "no due date".
            var example = new JsonObject();
            foreach (var property in schema.Properties?.Keys ?? Enumerable.Empty<string>())
            {
                var clr = context.Type.GetProperties().FirstOrDefault(p =>
                    string.Equals(p.Name, property, StringComparison.OrdinalIgnoreCase));
                if (clr is null) continue;
                if (clr.PropertyType == typeof(bool)) example[property] = false;
                else if (clr.PropertyType.IsValueType && Nullable.GetUnderlyingType(clr.PropertyType) is null) continue;
                else example[property] = null;
            }
            schema.Example = example;
        }
    }

    /// <summary>Every real property as null (bool false), collections as one neutral element; no sample data.</summary>
    private static JsonObject NeutralExample(Type type)
    {
        var example = new JsonObject();
        foreach (var p in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!p.CanWrite) continue;
            var name = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(p.Name);
            var t = p.PropertyType;
            if (t == typeof(bool)) example[name] = false;
            else if (t != typeof(string) && t.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(t)
                     && t.GetGenericArguments()[0] is { IsClass: true } item && item != typeof(string))
                example[name] = new JsonArray(NeutralExample(item));
            else if (t.IsValueType && Nullable.GetUnderlyingType(t) is null) continue;   // JSON null cannot bind
            else example[name] = null;
        }
        return example;
    }
}
