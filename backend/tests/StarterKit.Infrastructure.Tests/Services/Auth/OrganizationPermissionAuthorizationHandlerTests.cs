using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class OrganizationPermissionAuthorizationHandlerTests
{
    private const string PermissionCode = "organizations.members.manage";

    private sealed record Fixture(
        OrganizationPermissionAuthorizationHandler Handler,
        IPermissionResolver PermissionResolver);

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

        IPermissionResolver permissionResolver = Substitute.For<IPermissionResolver>();

        OrganizationPermissionAuthorizationHandler handler = new(accessor, currentUserService, permissionResolver);

        return new Fixture(handler, permissionResolver);
    }

    private static async Task<AuthorizationHandlerContext> AuthorizeAsync(
        OrganizationPermissionAuthorizationHandler handler, string permissionCode)
    {
        OrganizationPermissionRequirement requirement = new(permissionCode);
        AuthorizationHandlerContext context = new([requirement], new ClaimsPrincipal(), resource: null);

        await handler.HandleAsync(context);

        return context;
    }

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
    public async Task HandleAsync_PermissionAbsent_DoesNotSucceed()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Fixture f = CreateFixture(organizationId, accountId.ToString());
        f.PermissionResolver.GetEffectivePermissionsAsync(organizationId, accountId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlySet<string>)new HashSet<string>());

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_MissingRouteValue_DoesNotSucceedAndSkipsResolution()
    {
        Fixture f = CreateFixture(organizationId: null, Guid.NewGuid().ToString());

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.False(context.HasSucceeded);
        await f.PermissionResolver.DidNotReceive().GetEffectivePermissionsAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_UnparseableUserId_DoesNotSucceed()
    {
        Fixture f = CreateFixture(Guid.NewGuid(), userId: "not-a-guid");

        AuthorizationHandlerContext context = await AuthorizeAsync(f.Handler, PermissionCode);

        Assert.False(context.HasSucceeded);
    }
}
