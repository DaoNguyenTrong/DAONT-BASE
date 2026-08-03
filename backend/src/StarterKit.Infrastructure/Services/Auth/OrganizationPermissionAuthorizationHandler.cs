using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

internal sealed class OrganizationPermissionAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserService currentUserService,
    IPermissionResolver permissionResolver) : AuthorizationHandler<OrganizationPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OrganizationPermissionRequirement requirement)
    {
        if (!RouteOrganizationId.TryGet(httpContextAccessor, out Guid organizationId) ||
            !Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            return;
        }

        IReadOnlySet<string> permissions = await permissionResolver.GetEffectivePermissionsAsync(
            organizationId, accountId, httpContextAccessor.HttpContext!.RequestAborted);

        if (permissions.Contains(requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }
}
