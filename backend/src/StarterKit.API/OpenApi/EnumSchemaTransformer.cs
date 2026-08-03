using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StarterKit.API.OpenApi;

/// <summary>
/// The built-in OpenAPI schema generator is System.Text.Json-based and has no visibility into the
/// app's actual JSON formatter (Newtonsoft, configured with a global StringEnumConverter in
/// ConfigureNewtonsoftJsonOptions) — so every enum defaults to a bare `integer` schema, which
/// doesn't match what the API actually sends/accepts on the wire. Rewrites enum schemas to the
/// named-string shape that matches real runtime behavior, so generated clients (orval) produce a
/// string union instead of `number`.
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
