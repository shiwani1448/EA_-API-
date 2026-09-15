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
            schema.Example = new JsonObject
            {
                ["intakeRequestId"] = null,
                ["workflowInstanceId"] = null,
                ["businessModuleId"] = null,
                ["businessRecordId"] = null,
                ["subject"] = "Vendor quotation follow-up",
                ["type"] = "General",
                ["note"] = "Follow up with vendor regarding pending quotation",
                ["assignedToId"] = "TEST-USER",
                ["assignedToName"] = "Test User",
                ["priorityLevelId"] = null,
                ["dueAt"] = "2026-09-10T12:00:00Z",
                ["reminderAt"] = "2026-09-09T09:00:00Z",
                ["nextFollowupAt"] = "2026-09-09T12:00:00Z",
                ["waitingOnId"] = null,
                ["waitingOnName"] = "Vendor",
                ["waitingOnExternal"] = "External Vendor",
                ["responseOwnerId"] = null,
                ["responseOwnerName"] = "Vendor",
                ["expectedResponseAt"] = "2026-09-10T10:00:00Z",
                ["sequenceNumber"] = null
            };
        }
    }
}
