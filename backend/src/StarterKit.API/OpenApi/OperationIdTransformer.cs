using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace StarterKit.API.OpenApi;

/// <summary>
/// Sets a stable, predictable OperationId (ControllerName_ActionName) instead of the
/// auto-generated one derived from the route — keeps generated frontend client function names
/// readable (e.g. `accountsGetAll` instead of a path-mangled fallback).
/// </summary>
public sealed class OperationIdTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            operation.OperationId = $"{descriptor.ControllerName}_{descriptor.ActionName}";
        }

        return Task.CompletedTask;
    }
}
