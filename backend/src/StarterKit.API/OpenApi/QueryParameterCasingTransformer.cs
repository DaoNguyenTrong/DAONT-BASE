using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StarterKit.API.OpenApi;

/// <summary>
/// Query parameters bound from a shared request DTO (e.g. paging: PageNumber/PageSize/Search)
/// reflect the DTO's PascalCase C# property names, while parameters bound directly as method
/// arguments are already camelCase — an inconsistency baked into the declared contract. ASP.NET
/// Core's query-string binding is case-insensitive so this never affected the real server, but
/// it did make client codegen emit PascalCase param names inconsistent with this app's actual
/// (camelCase) query-string convention. Normalize the declared casing so the generated client
/// matches what every hand-written caller has always sent.
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
