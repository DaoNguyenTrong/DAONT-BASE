using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class OrganizationMembershipAuthorizationHandlerTests
{
    private sealed record Fixture(
        OrganizationMembershipAuthorizationHandler Handler,
        ITenantAccessService TenantAccessService);

    private static Fixture CreateFixture(Guid? organizationId, string? userId)
    {
        DefaultHttpContext httpContext = new();

        if (organizationId is { } orgId)
        {
            httpContext.Request.RouteValues = new RouteValueDictionary { ["id"] = orgId.ToString() };
        }

        HttpContextAccessor accessor = new() { HttpContext = httpContext };

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userId);

        ITenantAccessService tenantAccessService = Substitute.For<ITenantAccessService>();

        OrganizationMembershipAuthorizationHandler handler = new(accessor, currentUserService, tenantAccessService);

        return new Fixture(handler, tenantAccessService);
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(
        OrganizationMembershipAuthorizationHandler handler)
    {
        OrganizationMembershipRequirement requirement = new();
        AuthorizationHandlerContext context = new([requirement], new ClaimsPrincipal(), resource: null);

        await handler.HandleAsync(context);

        return context;
    }

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
    public async Task HandleAsync_NotAMember_DoesNotSucceed()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(organizationId, accountId.ToString());
        f.TenantAccessService.HasActiveAccessAsync(accountId, organizationId, Arg.Any<CancellationToken>())
            .Returns(false);

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_MissingRouteValue_DoesNotSucceedAndSkipsResolution()
    {
        Fixture f = CreateFixture(organizationId: null, Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler);

        Assert.False(context.HasSucceeded);
        await f.TenantAccessService.DidNotReceive().HasActiveAccessAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UnparseableUserId_DoesNotSucceed()
    {
        Fixture f = CreateFixture(Guid.NewGuid(), userId: "not-a-guid");

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler);

        Assert.False(context.HasSucceeded);
    }
}
