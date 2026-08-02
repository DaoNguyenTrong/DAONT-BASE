using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(
        string[]? audiences = null,
        int accessTokenExpiryMinutes = 15)
    {
        IOptions<JwtSettings> options = Options.Create(new JwtSettings
        {
            SecretKey = "this-is-a-sufficiently-long-test-secret-key-1234567890",
            Issuer = "StarterKit.Tests",
            Audiences = audiences ?? ["StarterKit.Tests.Client"],
            AccessTokenExpiryMinutes = accessTokenExpiryMinutes
        });

        return new JwtTokenService(options);
    }

    private static Account CreateAccount() =>
        Account.Create(new AccountParams("Nguyen Van A", "nva", "nva@example.com"));

    // GenerateAccessToken

    [Fact]
    public void GenerateAccessToken_NoAudiences_ThrowsInvalidOperation()
    {
        JwtTokenService service = CreateService(audiences: []);
        Account account = CreateAccount();

        Assert.Throws<InvalidOperationException>(() => service.GenerateAccessToken(account, null));
    }

    [Fact]
    public void GenerateAccessToken_ValidSettings_ProducesTokenWithExpectedClaims()
    {
        JwtTokenService service = CreateService();
        Account account = CreateAccount();

        string token = service.GenerateAccessToken(account, null);

        JwtSecurityToken parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(account.Id.ToString(), parsed.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(account.Username, parsed.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal(account.Email, parsed.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("StarterKit.Tests", parsed.Issuer);
        Assert.Contains("StarterKit.Tests.Client", parsed.Audiences);
        Assert.DoesNotContain(parsed.Claims, c => c.Type == IJwtTokenService.OrganizationIdClaimType);
        double diffSeconds = Math.Abs((parsed.ValidTo - DateTime.UtcNow.AddMinutes(15)).TotalSeconds);
        Assert.True(diffSeconds < 5, $"Expected expiry within 5s tolerance, was off by {diffSeconds}s");
    }

    [Fact]
    public void GenerateAccessToken_WithOrganizationId_IncludesOrganizationClaim()
    {
        JwtTokenService service = CreateService();
        Account account = CreateAccount();
        Guid organizationId = Guid.NewGuid();

        string token = service.GenerateAccessToken(account, organizationId);

        JwtSecurityToken parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(
            organizationId.ToString(),
            parsed.Claims.First(c => c.Type == IJwtTokenService.OrganizationIdClaimType).Value);
    }

    [Fact]
    public void GenerateAccessToken_MultipleAudiences_UsesFirstAsTokenAudience()
    {
        JwtTokenService service = CreateService(audiences: ["first-audience", "second-audience"]);
        Account account = CreateAccount();

        string token = service.GenerateAccessToken(account, null);

        JwtSecurityToken parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains("first-audience", parsed.Audiences);
        Assert.DoesNotContain("second-audience", parsed.Audiences);
    }

    // GenerateRefreshToken

    [Fact]
    public void GenerateRefreshToken_ReturnsUrlSafeBase64WithoutPadding()
    {
        JwtTokenService service = CreateService();

        string token = service.GenerateRefreshToken();

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
        Assert.True(token.Length > 20);
    }

    [Fact]
    public void GenerateRefreshToken_TwoCalls_ProduceDifferentValues()
    {
        JwtTokenService service = CreateService();

        string first = service.GenerateRefreshToken();
        string second = service.GenerateRefreshToken();

        Assert.NotEqual(first, second);
    }
}
