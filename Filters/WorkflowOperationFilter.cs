using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
namespace Jarvis5.Filters;
public class WorkflowOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context) { }
}


// Swagger does not infer FluentValidation requirements automatically.
public class WorkflowRequestSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema definition, SchemaFilterContext context)
    {
        if (definition is not OpenApiSchema schema) return;
        var type = context.Type;
        string[] required;
        if (type == typeof(Jarvis5.Dtos.EaFms.StartWorkflowRequestDto))
        {
            required = new[] { "intakeRequestId" };
            schema.Description = "Create Captured Workflow from an existing IntakeRequest. Notes are optional initial lifecycle history context, not business details or execution start information.";
        }
        else if (type == typeof(Jarvis5.Dtos.EaFms.StartWorkRequestDto))
        {
            required = Array.Empty<string>();
            schema.Description = "Optional execution-start notes (maximum 2000 characters). All identity, target status and timestamps come from the path/server.";
        }
        else if (type == typeof(Jarvis5.Dtos.EaFms.CreateWaitingRequestDto))
        {
            required = new[] { "reason", "requestSentAt", "expectedResponseAt", "nextFollowupAt" };
            schema.Description = "Requires a reason, at least one waitingOn field, and responseOwnerId or responseOwnerName. Times must be UTC. Expected response and next follow-up must not precede requestSentAt. Next follow-up is stored in Followup; LastFollowupAt is not set by scheduling.";
        }
        else if (type == typeof(Jarvis5.Dtos.EaFms.ResumeWorkPauseRequestDto))
        {
            required = new[] { "targetStatusName" };
            schema.Description = "TargetStatusName accepts only In Progress or Submitted (case-insensitive). ResumedReason is optional, maximum 2000 characters. No numeric status IDs or timestamps are accepted.";
        }
        else if (type == typeof(Jarvis5.Dtos.EaFms.CompleteWorkRequestDto))
        {
            required = new[] { "notes" };
            schema.Description = "Final notes are required, maximum 2000 characters. EvidenceAttachmentIds defaults to an empty list; IDs must be positive. Nonempty selections are currently blocked with 409 because no deterministic EA closure-evidence convention exists. No evidence is silently discarded.";
        }
        else return;
        schema.Required ??= new HashSet<string>();
        foreach (var name in required)
        {
            schema.Required.Add(name);
            if (schema.Properties?.TryGetValue(name, out var field) == true && field is OpenApiSchema property && property.Type.HasValue)
                property.Type &= ~JsonSchemaType.Null;
        }
    }
}
