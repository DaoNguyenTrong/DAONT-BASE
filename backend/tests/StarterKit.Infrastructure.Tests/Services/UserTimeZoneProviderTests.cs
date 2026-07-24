using Microsoft.AspNetCore.Http;
using StarterKit.Infrastructure.Services;

namespace StarterKit.Infrastructure.Tests.Services;

public class UserTimeZoneProviderTests
{
    private static UserTimeZoneProvider CreateProvider(DefaultHttpContext? httpContext)
    {
        HttpContextAccessor accessor = new() { HttpContext = httpContext };
        return new UserTimeZoneProvider(accessor);
    }

    [Fact]
    public void UserTimeZone_NoHttpContext_ReturnsUtc()
    {
        UserTimeZoneProvider provider = CreateProvider(null);

        Assert.Equal(TimeZoneInfo.Utc, provider.UserTimeZone);
    }

    [Fact]
    public void UserTimeZone_ValidHeader_ResolvesTimeZone()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-TimeZone"] = "Asia/Ho_Chi_Minh";
        UserTimeZoneProvider provider = CreateProvider(httpContext);

        Assert.Equal("Asia/Ho_Chi_Minh", provider.TimeZoneId);
    }

    [Fact]
    public void UserTimeZone_InvalidHeader_FallsBackToUtc()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-TimeZone"] = "Not/A_Real_Zone";
        UserTimeZoneProvider provider = CreateProvider(httpContext);

        Assert.Equal(TimeZoneInfo.Utc, provider.UserTimeZone);
    }

    [Fact]
    public void UserTimeZone_PreSetInItems_IsReturnedDirectly()
    {
        DefaultHttpContext httpContext = new();
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        httpContext.Items["UserTimeZone"] = zone;
        UserTimeZoneProvider provider = CreateProvider(httpContext);

        Assert.Equal(zone, provider.UserTimeZone);
    }

    [Fact]
    public void UserTimeZone_ComputedOnce_MemoizedAcrossAccesses()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-TimeZone"] = "Asia/Ho_Chi_Minh";
        UserTimeZoneProvider provider = CreateProvider(httpContext);

        TimeZoneInfo first = provider.UserTimeZone;
        httpContext.Request.Headers["X-TimeZone"] = "UTC";
        TimeZoneInfo second = provider.UserTimeZone;

        Assert.Equal(first, second);
    }

    [Fact]
    public void ConvertToUtc_ThenConvertFromUtc_RoundTrips()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-TimeZone"] = "Asia/Ho_Chi_Minh";
        UserTimeZoneProvider provider = CreateProvider(httpContext);
        DateTime local = new(2026, 7, 24, 10, 0, 0);

        DateTime utc = provider.ConvertToUtc(local);
        DateTime backToLocal = provider.ConvertFromUtc(utc);

        Assert.Equal(local, backToLocal);
        Assert.Equal(7, (local - utc).Hours);
    }
}
