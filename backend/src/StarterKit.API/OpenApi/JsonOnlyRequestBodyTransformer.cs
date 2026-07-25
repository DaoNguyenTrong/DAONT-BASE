using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StarterKit.API.OpenApi;

/// <summary>
/// ASP.NET Core's default input formatter advertises 4 near-identical JSON media types
/// (`application/json`, `text/json`, `application/*+json`, `application/json-patch+json`) for
/// every request body, none of which are actually JSON Patch — trim to `application/json` so
/// client codegen (orval) doesn't pick an arbitrary one (it was defaulting to
/// `application/json-patch+json`, which is misleading even though the server accepts it).
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
