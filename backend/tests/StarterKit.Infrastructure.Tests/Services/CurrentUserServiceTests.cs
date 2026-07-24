using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StarterKit.Infrastructure.Services;

namespace StarterKit.Infrastructure.Tests.Services;

public class CurrentUserServiceTests
{
    private static CurrentUserService CreateService(DefaultHttpContext? httpContext)
    {
        HttpContextAccessor accessor = new() { HttpContext = httpContext };
        return new CurrentUserService(accessor);
    }

    private static DefaultHttpContext CreateHttpContextWithClaims(params Claim[] claims)
    {
        ClaimsIdentity identity = new(claims, "TestAuth");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    [Fact]
    public void NoHttpContext_AllValuesAreNullOrFalse()
    {
        CurrentUserService service = CreateService(null);

        Assert.Null(service.UserId);
        Assert.Null(service.UserName);
        Assert.False(service.IsApiKeyCaller);
        Assert.Null(service.ApiKeyId);
    }

    [Fact]
    public void UserIdAndUserName_MapFromClaims()
    {
        Guid accountId = Guid.NewGuid();
        DefaultHttpContext httpContext = CreateHttpContextWithClaims(
            new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
            new Claim(ClaimTypes.Name, "nva"));
        CurrentUserService service = CreateService(httpContext);

        Assert.Equal(accountId.ToString(), service.UserId);
        Assert.Equal("nva", service.UserName);
    }

    [Fact]
    public void ApiKeyClaim_Present_IsApiKeyCallerTrue_AndApiKeyIdParsed()
    {
        Guid keyId = Guid.NewGuid();
        DefaultHttpContext httpContext = CreateHttpContextWithClaims(
            new Claim(ApiKeyClaims.KeyId, keyId.ToString()));
        CurrentUserService service = CreateService(httpContext);

        Assert.True(service.IsApiKeyCaller);
        Assert.Equal(keyId, service.ApiKeyId);
    }

    [Fact]
    public void ApiKeyClaim_Absent_IsApiKeyCallerFalse_AndApiKeyIdNull()
    {
        DefaultHttpContext httpContext = CreateHttpContextWithClaims(
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        CurrentUserService service = CreateService(httpContext);

        Assert.False(service.IsApiKeyCaller);
        Assert.Null(service.ApiKeyId);
    }

    [Fact]
    public void ApiKeyClaim_NonGuidValue_ApiKeyIdNull()
    {
        DefaultHttpContext httpContext = CreateHttpContextWithClaims(
            new Claim("api-key-id", "not-a-guid"));
        CurrentUserService service = CreateService(httpContext);

        Assert.True(service.IsApiKeyCaller);
        Assert.Null(service.ApiKeyId);
    }
}
