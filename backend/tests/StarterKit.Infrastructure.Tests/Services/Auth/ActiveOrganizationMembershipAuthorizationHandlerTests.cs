using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class ActiveOrganizationMembershipAuthorizationHandlerTests
{
    private sealed record Fixture(
        ActiveOrganizationMembershipAuthorizationHandler Handler,
        ITenantAccessService TenantAccessService);

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

        ITenantAccessService tenantAccessService = Substitute.For<ITenantAccessService>();

        ActiveOrganizationMembershipAuthorizationHandler handler = new(
            accessor, currentTenantProvider, currentUserService, tenantAccessService);

        return new Fixture(handler, tenantAccessService);
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(
        ActiveOrganizationMembershipAuthorizationHandler handler)
    {
        ActiveOrganizationMembershipRequirement requirement = new();
        AuthorizationHandlerContext context = new([requirement], new ClaimsPrincipal(), resource: null);

        await handler.HandleAsync(context);

        return context;
    }

    // HandleAsync

    [Fact]
    public async Task HandleAsync_ActiveMember_Succeeds()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(organizationId, accountId.ToString());
        f.TenantAccessService.HasActiveAccessAsync(accountId, organizationId, Arg.Any<CancellationToken>())
            .Returns(true);

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_NoActiveAccess_DoesNotSucceed()
    {
        // HasActiveAccessAsync returning false covers non-members as well as inactive/revoked
        // memberships — that distinction lives in TenantAccessService (see TenantAccessServiceTests).
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(organizationId, accountId.ToString());
        f.TenantAccessService.HasActiveAccessAsync(accountId, organizationId, Arg.Any<CancellationToken>())
            .Returns(false);

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_NoActiveOrganizationClaim_DoesNotSucceedAndSkipsResolution()
    {
        Fixture f = CreateFixture(activeOrganizationId: null, Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler);

        Assert.False(context.HasSucceeded);
        await f.TenantAccessService.DidNotReceive().HasActiveAccessAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData(null)]
    public async Task HandleAsync_UnparseableUserId_DoesNotSucceedAndSkipsResolution(string? userId)
    {
        Fixture f = CreateFixture(Guid.NewGuid(), userId);

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler);

        Assert.False(context.HasSucceeded);
        await f.TenantAccessService.DidNotReceive().HasActiveAccessAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RouteIdDiffersFromClaim_ResolvesAgainstClaimOrganization()
    {
        Guid claimOrganizationId = Guid.NewGuid();
        Guid routeOrganizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(claimOrganizationId, accountId.ToString(), routeOrganizationId);
        f.TenantAccessService.HasActiveAccessAsync(accountId, claimOrganizationId, Arg.Any<CancellationToken>())
            .Returns(true);

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler);

        Assert.True(context.HasSucceeded);
        await f.TenantAccessService.DidNotReceive().HasActiveAccessAsync(
            Arg.Any<Guid>(), routeOrganizationId, Arg.Any<CancellationToken>());
    }
}
