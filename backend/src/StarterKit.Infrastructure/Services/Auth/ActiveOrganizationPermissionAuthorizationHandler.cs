using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

/// <summary>
/// Claims-based counterpart to <see cref="OrganizationPermissionAuthorizationHandler"/> — resolves
/// the organization from <see cref="ICurrentTenantProvider"/> (the JWT <c>org_id</c> claim) instead
/// of an <c>{id}</c> route segment. See <see cref="ActiveOrganizationMembershipAuthorizationHandler"/>
/// for why these resources aren't nested under <c>/organizations/{id}</c>.
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
