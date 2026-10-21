using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.API.Middleware;

public sealed class TenantAccessMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerSettings ProblemJsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUserService,
        ICurrentTenantProvider currentTenantProvider,
        ITenantAccessService tenantAccessService)
    {
        // Exempt, as gating on the active org claim would 403 them once that org's access is revoked: /api/auth
        // (logout and switch-organization must still work), /api/organizations (self-authorizes against the org id
        // in its URL; gating would block cross-org actions) and /api/permissions (static, org-independent catalog).
        if (context.Request.Path.StartsWithSegments("/api/auth")
            || context.Request.Path.StartsWithSegments("/api/organizations")
            || context.Request.Path.StartsWithSegments("/api/permissions")
            || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        Guid? organizationId = currentTenantProvider.OrganizationId;

        if (organizationId is null)
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId) ||
            !await tenantAccessService.HasActiveAccessAsync(accountId, organizationId.Value, context.RequestAborted))
        {
            await WriteProblemAsync(context);
            return;
        }

        await next(context);
    }

    private static Task WriteProblemAsync(HttpContext context)
    {
        ProblemDetails problemDetails = new()
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "Organization access is no longer valid."
        };

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";

        return context.Response.WriteAsync(JsonConvert.SerializeObject(problemDetails, ProblemJsonSettings));
    }
}
