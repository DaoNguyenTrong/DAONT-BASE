using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StarterKit.API.OpenApi;

/// <summary>
/// Adds the required X-TimeZone header to all OpenAPI operations.
/// </summary>
public sealed class TimeZoneHeaderOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-TimeZone",
            In = ParameterLocation.Header,
            Required = true,
            Description = "IANA timezone identifier used to format date/time values. Examples: Asia/Ho_Chi_Minh, UTC, America/New_York.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Example = JsonValue.Create("Asia/Ho_Chi_Minh")
            }
        });

        return Task.CompletedTask;
    }
}
