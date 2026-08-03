using Microsoft.AspNetCore.Http;

namespace StarterKit.Infrastructure.Services.Auth;

internal static class RouteOrganizationId
{
    public static bool TryGet(IHttpContextAccessor httpContextAccessor, out Guid organizationId)
    {
        object? value = httpContextAccessor.HttpContext?.Request.RouteValues["id"];

        return Guid.TryParse(value?.ToString(), out organizationId);
    }
}
