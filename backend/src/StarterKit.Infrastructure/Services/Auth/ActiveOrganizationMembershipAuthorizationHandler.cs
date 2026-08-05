using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

/// <summary>
/// Claims-based counterpart to <see cref="OrganizationMembershipAuthorizationHandler"/> — resolves
/// the organization from <see cref="ICurrentTenantProvider"/> (the JWT <c>org_id</c> claim) instead
/// of an <c>{id}</c> route segment, for resources scoped to the caller's active organization
/// (Files, ApiKeys, AuditLogs, SystemSettings) rather than nested under <c>/organizations/{id}</c>.
/// No active organization (Personal context, <c>org_id</c> claim absent) fails closed.
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
