using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class ActiveOrganizationPermissionAuthorizationHandlerTests
{
    private const string PermissionCode = Permissions.FilesManage;

    private sealed record Fixture(
        ActiveOrganizationPermissionAuthorizationHandler Handler,
        IPermissionResolver PermissionResolver);

    private static Fixture CreateFixture(Guid? activeOrganizationId, string? userId, Guid? routeOrganizationId = null)
    {
        DefaultHttpContext httpContext = new();

        if (routeOrganizationId is { } routeOrgId)
        {
            httpContext.Request.RouteValues = new RouteValueDictionary { ["id"] = routeOrgId.ToString() };
        }

        HttpContextAccessor accessor = new() { HttpContext = httpContext };

        ICurrentTenantProvider currentTenantProvider = Substitute.For<ICurrentTenantProvider>();
        currentTenantProvider.OrganizationId.Returns(activeOrganizationId);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userId);

        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();

        ActiveOrganizationPermissionAuthorizationHandler handler = new(
            accessor, currentTenantProvider, currentUserService, permissionResolver);

        return new Fixture(handler, permissionResolver);
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(
        ActiveOrganizationPermissionAuthorizationHandler handler, string permissionCode)
    {
        ActiveOrganizationPermissionRequirement requirement = new(permissionCode);
        AuthorizationHandlerContext context = new([requirement], new ClaimsPrincipal(), resource: null);

        await handler.HandleAsync(context);

        return context;
    }

    // HandleAsync

    [Fact]
    public async Task HandleAsync_PermissionPresent_Succeeds()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(organizationId, accountId.ToString());
        f.PermissionResolver.GetEffectivePermissionsAsync(organizationId, accountId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string> { PermissionCode });

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_NoPermissions_DoesNotSucceed()
    {
        // An empty set is what the resolver returns for a non-member or an inactive/revoked
        // membership (see PermissionResolverTests).
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(organizationId, accountId.ToString());
        f.PermissionResolver.GetEffectivePermissionsAsync(organizationId, accountId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string>());

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_OnlyOtherPermissions_DoesNotSucceed()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(organizationId, accountId.ToString());
        f.PermissionResolver.GetEffectivePermissionsAsync(organizationId, accountId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string> { Permissions.ApiKeysManage, Permissions.AuditLogsView });

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_NoActiveOrganizationClaim_DoesNotSucceedAndSkipsResolution()
    {
        Fixture f = CreateFixture(activeOrganizationId: null, Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.False(context.HasSucceeded);
        await f.PermissionResolver.DidNotReceive().GetEffectivePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData(null)]
    public async Task HandleAsync_UnparseableUserId_DoesNotSucceedAndSkipsResolution(string? userId)
    {
        Fixture f = CreateFixture(Guid.NewGuid(), userId);

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.False(context.HasSucceeded);
        await f.PermissionResolver.DidNotReceive().GetEffectivePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RouteIdDiffersFromClaim_ResolvesAgainstClaimOrganization()
    {
        Guid claimOrganizationId = Guid.NewGuid();
        Guid routeOrganizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(claimOrganizationId, accountId.ToString(), routeOrganizationId);
        f.PermissionResolver.GetEffectivePermissionsAsync(claimOrganizationId, accountId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string> { PermissionCode });

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.True(context.HasSucceeded);
        await f.PermissionResolver.DidNotReceive().GetEffectivePermissionsAsync(
            routeOrganizationId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
