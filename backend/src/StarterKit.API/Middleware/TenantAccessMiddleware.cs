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
        // Exempt: /api/auth (must work even when the session's active org access is revoked, e.g.
        // logout/switch-organization), /api/organizations (its endpoints take an explicit
        // organization id in the URL and self-authorize against it — they aren't scoped to
        // the session's active org claim, so gating them on it would block cross-org actions,
        // e.g. managing org B while the token's active org A has been revoked), and
        // /api/permissions (a static, org-independent catalog — gating it on the active org
        // claim would spuriously 403 it the moment that org's access is revoked).
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
        var problemDetails = new ProblemDetails
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
