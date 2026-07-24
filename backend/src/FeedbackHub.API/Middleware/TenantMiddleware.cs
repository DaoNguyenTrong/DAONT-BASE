using System.Security.Claims;
using FeedbackHub.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FeedbackHub.API.Middleware;

public sealed class TenantMiddleware(RequestDelegate next)
{
    private const string TenantHeaderName = "X-Tenant-Id";
    public const string TenantIdItemKey = "TenantId";
    public const string TenantRoleItemKey = "TenantRole";

    public async Task InvokeAsync(HttpContext context, ITenantContextResolver tenantContextResolver)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValues)
            || !Guid.TryParse(headerValues.ToString(), out Guid tenantId)
            || !Guid.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Guid accountId))
        {
            await next(context);
            return;
        }

        var result = await tenantContextResolver.ResolveAsync(tenantId, accountId, context.RequestAborted);

        if (result.HasValue)
        {
            context.Items[TenantIdItemKey] = result.Value.TenantId;
            context.Items[TenantRoleItemKey] = result.Value.Role;
        }

        await next(context);
    }
}
