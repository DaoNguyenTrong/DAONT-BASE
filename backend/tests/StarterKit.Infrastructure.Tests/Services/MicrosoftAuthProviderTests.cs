using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Domain.Exceptions;
using StarterKit.Infrastructure.Services;

namespace StarterKit.Infrastructure.Tests.Services;

public class MicrosoftAuthProviderTests
{
    private sealed record Fixture(MicrosoftAuthProvider Provider, IMicrosoftJwtValidator Validator);

    private static Fixture CreateFixture(string clientId = "test-client-id", string tenantId = "common")
    {
        IOptions<ExternalAuthSettings> options = Options.Create(new ExternalAuthSettings
        {
            Microsoft = new MicrosoftAuthSettings { ClientId = clientId, TenantId = tenantId }
        });
        IMicrosoftJwtValidator validator = Substitute.For<IMicrosoftJwtValidator>();

        MicrosoftAuthProvider provider = new(options, validator);

        return new Fixture(provider, validator);
    }

    [Fact]
    public void ProviderName_IsMicrosoft()
    {
        Fixture f = CreateFixture();

        Assert.Equal("Microsoft", f.Provider.ProviderName);
    }

    [Fact]
    public async Task ValidateAsync_ValidPayload_MapsToExternalUserInfo()
    {
        Fixture f = CreateFixture();
        MicrosoftTokenPayload payload = new("microsoft-oid-1", "user@example.com", "Test User");
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(payload);

        ExternalUserInfo result = await f.Provider.ValidateAsync("some-credential", CancellationToken.None);

        Assert.Equal("microsoft-oid-1", result.ProviderUserId);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("Test User", result.Name);
        Assert.True(result.EmailVerified);
    }

    [Fact]
    public async Task ValidateAsync_PassesConfiguredTenantAndClientId()
    {
        Fixture f = CreateFixture(clientId: "configured-client-id", tenantId: "configured-tenant-id");
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MicrosoftTokenPayload("sub", "e@example.com", null));

        await f.Provider.ValidateAsync("some-credential", CancellationToken.None);

        await f.Validator.Received(1).ValidateAsync(
            "some-credential", "configured-tenant-id", "configured-client-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_SecurityTokenException_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<MicrosoftTokenPayload>(_ => throw new SecurityTokenInvalidIssuerException("bad issuer"));

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Provider.ValidateAsync("bad-credential", CancellationToken.None));
        Assert.Equal(ApplicationMessages.InvalidExternalCredential, ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_SecurityTokenMalformedException_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<MicrosoftTokenPayload>(_ => throw new SecurityTokenMalformedException("not a JWT"));

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Provider.ValidateAsync("garbage-credential", CancellationToken.None));
        Assert.Equal(ApplicationMessages.InvalidExternalCredential, ex.Message);
    }

    [Fact]
    public async Task ValidateAsync_NoEmailClaim_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MicrosoftTokenPayload("sub", null, "Test User"));

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Provider.ValidateAsync("some-credential", CancellationToken.None));
        Assert.Equal(ApplicationMessages.InvalidExternalCredential, ex.Message);
    }
}
