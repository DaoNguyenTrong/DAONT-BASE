using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StarterKit.API.OpenApi;

/// <summary>
/// Query parameters bound from a shared request DTO (e.g. paging) get its PascalCase property names; ones bound as
/// method arguments are camelCase. Binding is case-insensitive, so only the contract differs: normalize it to
/// camelCase so the generated client follows the app's query-string convention.
/// </summary>
public sealed class QueryParameterCasingTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        foreach (OpenApiParameter parameter in operation.Parameters)
        {
            if (parameter.In == ParameterLocation.Query && !string.IsNullOrEmpty(parameter.Name))
            {
                parameter.Name = char.ToLowerInvariant(parameter.Name[0]) + parameter.Name[1..];
            }
        }

        return Task.CompletedTask;
    }
}
