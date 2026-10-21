using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

/// <summary>
/// Like <see cref="OrganizationPermissionAuthorizationHandler"/>, but the organization comes from the JWT <c>org_id</c>
/// claim (<see cref="ICurrentTenantProvider"/>), not an <c>{id}</c> route segment. Why these resources aren't under
/// <c>/organizations/{id}</c>: see <see cref="ActiveOrganizationMembershipAuthorizationHandler"/>.
/// </summary>
internal sealed class ActiveOrganizationPermissionAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentUserService currentUserService,
    IPermissionResolver permissionResolver) : AuthorizationHandler<ActiveOrganizationPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ActiveOrganizationPermissionRequirement requirement)
    {
        if (currentTenantProvider.OrganizationId is not { } organizationId ||
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
