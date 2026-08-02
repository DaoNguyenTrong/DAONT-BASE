using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class CurrentTenantProviderTests
{
    private static CurrentTenantProvider CreateService(DefaultHttpContext? httpContext)
    {
        HttpContextAccessor accessor = new() { HttpContext = httpContext };
        return new CurrentTenantProvider(accessor);
    }

    private static DefaultHttpContext CreateHttpContextWithClaims(params Claim[] claims)
    {
        ClaimsIdentity identity = new(claims, "TestAuth");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    [Fact]
    public void NoHttpContext_OrganizationIdIsNull()
    {
        CurrentTenantProvider service = CreateService(null);

        Assert.Null(service.OrganizationId);
    }

    [Fact]
    public void NoOrganizationClaim_OrganizationIdIsNull()
    {
        DefaultHttpContext httpContext = CreateHttpContextWithClaims(
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        CurrentTenantProvider service = CreateService(httpContext);

        Assert.Null(service.OrganizationId);
    }

    [Fact]
    public void OrganizationClaim_Present_ParsesOrganizationId()
    {
        Guid organizationId = Guid.NewGuid();
        DefaultHttpContext httpContext = CreateHttpContextWithClaims(
            new Claim(IJwtTokenService.OrganizationIdClaimType, organizationId.ToString()));
        CurrentTenantProvider service = CreateService(httpContext);

        Assert.Equal(organizationId, service.OrganizationId);
    }

    [Fact]
    public void OrganizationClaim_NonGuidValue_OrganizationIdIsNull()
    {
        DefaultHttpContext httpContext = CreateHttpContextWithClaims(
            new Claim(IJwtTokenService.OrganizationIdClaimType, "not-a-guid"));
        CurrentTenantProvider service = CreateService(httpContext);

        Assert.Null(service.OrganizationId);
    }
}
