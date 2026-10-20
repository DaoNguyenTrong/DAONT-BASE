using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StarterKit.API.OpenApi;

/// <summary>
/// The default input formatter advertises 4 near-identical JSON media types (`application/json`, `text/json`,
/// `application/*+json`, `application/json-patch+json`) for every request body, none of them JSON Patch. Trim to
/// `application/json` so orval doesn't pick an arbitrary one such as the misleading `application/json-patch+json`.
/// </summary>
public sealed class JsonOnlyRequestBodyTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.RequestBody?.Content is { } content && content.TryGetValue("application/json", out OpenApiMediaType? jsonMediaType))
        {
            content.Clear();
            content["application/json"] = jsonMediaType;
        }

        return Task.CompletedTask;
    }
}
