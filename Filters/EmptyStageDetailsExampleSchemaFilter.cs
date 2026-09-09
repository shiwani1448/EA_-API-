using Jarvis5.Dtos.DevelopmentPlan;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Jarvis5.Filters;

/// <summary>Swashbuckle auto-generates a one-item example for List&lt;T&gt; properties.
/// StageDetails on both the create and update module bodies is meant to be built
/// entirely by the frontend, so show it as an empty array in the Swagger example
/// rather than a placeholder object.</summary>
public class EmptyStageDetailsExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var declaringType = context.MemberInfo?.DeclaringType;
        var isStageDetails = context.MemberInfo?.Name == nameof(CreateModuleDto.StageDetails);

        if (isStageDetails && (declaringType == typeof(CreateModuleDto) || declaringType == typeof(UpdateTaskModuleDto)))
            schema.Example = new OpenApiArray();
    }
}
