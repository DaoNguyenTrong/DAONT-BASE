using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

internal sealed class OrganizationMembershipAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ICurrentUserService currentUserService,
    ITenantAccessService tenantAccessService) : AuthorizationHandler<OrganizationMembershipRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OrganizationMembershipRequirement requirement)
    {
        if (!RouteOrganizationId.TryGet(httpContextAccessor, out Guid organizationId) ||
            !Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            return;
        }

        bool hasAccess = await tenantAccessService.HasActiveAccessAsync(
            accountId, organizationId, httpContextAccessor.HttpContext!.RequestAborted);

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
    }
}
