using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

/// <summary>
/// Like <see cref="OrganizationMembershipAuthorizationHandler"/>, but the organization comes from the JWT <c>org_id</c>
/// claim (<see cref="ICurrentTenantProvider"/>), not an <c>{id}</c> route segment — for active-org resources (Files,
/// ApiKeys, AuditLogs, SystemSettings). No active organization (Personal context, no <c>org_id</c>) fails closed.
/// </summary>
internal sealed class ActiveOrganizationMembershipAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ICurrentTenantProvider currentTenantProvider,
    ICurrentUserService currentUserService,
    ITenantAccessService tenantAccessService) : AuthorizationHandler<ActiveOrganizationMembershipRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ActiveOrganizationMembershipRequirement requirement)
    {
        if (currentTenantProvider.OrganizationId is not { } organizationId ||
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
