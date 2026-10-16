using Microsoft.AspNetCore.Http;
using StarterKit.API.Common;
using StarterKit.Application.Common.Settings;

namespace StarterKit.API.Tests.Common;

public class AuthCookiesTests
{
    [Fact]
    public void CreateOptions_DefaultSettings_IsSecureCrossSite()
    {
        CookieOptions options = AuthCookies.CreateOptions(new AuthCookieSettings());

        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.None, options.SameSite);
        Assert.True(options.HttpOnly);
        Assert.Equal("/", options.Path);
    }

    [Fact]
    public void CreateOptions_PlainHttpDeployment_DropsSecureAndKeepsHttpOnly()
    {
        CookieOptions options = AuthCookies.CreateOptions(new AuthCookieSettings { Secure = false, SameSite = "Lax" });

        Assert.False(options.Secure);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
        Assert.True(options.HttpOnly);
    }

    [Theory]
    [InlineData("none", SameSiteMode.None)]
    [InlineData("Lax", SameSiteMode.Lax)]
    [InlineData("STRICT", SameSiteMode.Strict)]
    public void ParseSameSite_KnownValue_IsCaseInsensitive(string value, SameSiteMode expected)
    {
        Assert.Equal(expected, AuthCookies.ParseSameSite(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Loose")]
    // A real SameSiteMode member, but ASP.NET Core then writes no SameSite attribute at all.
    [InlineData("Unspecified")]
    // Enum.TryParse accepts any integer string, defined member or not.
    [InlineData("1")]
    [InlineData("42")]
    public void ParseSameSite_UnknownValue_Throws(string value)
    {
        Assert.Throws<InvalidOperationException>(() => AuthCookies.ParseSameSite(value));
    }

    [Fact]
    public void Validate_NotSecureWithSameSiteNone_Throws()
    {
        // Browsers reject SameSite=None without Secure: login would return 200 and store nothing.
        Assert.Throws<InvalidOperationException>(
            () => AuthCookies.Validate(new AuthCookieSettings { Secure = false, SameSite = "None" }));
    }

    [Theory]
    [InlineData(true, "None")]
    [InlineData(true, "Lax")]
    [InlineData(false, "Lax")]
    [InlineData(false, "Strict")]
    public void Validate_SupportedCombination_DoesNotThrow(bool secure, string sameSite)
    {
        AuthCookies.Validate(new AuthCookieSettings { Secure = secure, SameSite = sameSite });
    }
}
