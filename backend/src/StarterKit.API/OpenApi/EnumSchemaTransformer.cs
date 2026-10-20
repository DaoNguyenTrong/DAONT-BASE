using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StarterKit.API.OpenApi;

/// <summary>
/// The built-in schema generator uses System.Text.Json and can't see the app's global Newtonsoft StringEnumConverter
/// (ConfigureNewtonsoftJsonOptions), so it gives each enum a bare `integer` schema that doesn't match the wire.
/// Rewrites enum schemas to the named strings actually sent/accepted, so orval emits a string union, not `number`.
/// </summary>
public sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        Type underlyingType = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

        if (!underlyingType.IsEnum)
        {
            return Task.CompletedTask;
        }

        schema.Type = JsonSchemaType.String;
        schema.Enum = Enum.GetNames(underlyingType).Select(name => (JsonNode)name).ToList();

        return Task.CompletedTask;
    }
}
