using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Domain.Exceptions;
using StarterKit.Infrastructure.Services.Auth.External;

namespace StarterKit.Infrastructure.Tests.Services.Auth.External;

public class GoogleAuthProviderTests
{
    private sealed record Fixture(GoogleAuthProvider Provider, IGoogleJwtValidator Validator);

    private static Fixture CreateFixture(string clientId = "test-client-id")
    {
        IOptions<ExternalAuthSettings> options = Options.Create(new ExternalAuthSettings
        {
            Google = new GoogleAuthSettings { ClientId = clientId }
        });
        IGoogleJwtValidator validator = Substitute.For<IGoogleJwtValidator>();

        GoogleAuthProvider provider = new(options, validator);

        return new Fixture(provider, validator);
    }

    [Fact]
    public void ProviderName_IsGoogle()
    {
        Fixture f = CreateFixture();

        Assert.Equal("Google", f.Provider.ProviderName);
    }

    [Fact]
    public async Task ValidateAsync_ValidPayload_MapsToExternalUserInfo()
    {
        Fixture f = CreateFixture();
        GoogleJsonWebSignature.Payload payload = new()
        {
            Subject = "google-sub-1",
            Email = "user@example.com",
            Name = "Test User",
            EmailVerified = true
        };
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<GoogleJsonWebSignature.ValidationSettings>())
            .Returns(payload);

        ExternalUserInfo result = await f.Provider.ValidateAsync("some-credential", CancellationToken.None);

        Assert.Equal("google-sub-1", result.ProviderUserId);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("Test User", result.Name);
        Assert.True(result.EmailVerified);
    }

    [Fact]
    public async Task ValidateAsync_PassesConfiguredClientIdAsAudience()
    {
        Fixture f = CreateFixture(clientId: "configured-client-id");
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<GoogleJsonWebSignature.ValidationSettings>())
            .Returns(new GoogleJsonWebSignature.Payload { Subject = "sub", Email = "e@example.com" });

        await f.Provider.ValidateAsync("some-credential", CancellationToken.None);

        await f.Validator.Received(1).ValidateAsync(
            "some-credential",
            Arg.Is<GoogleJsonWebSignature.ValidationSettings>(s => s != null && s.Audience!.Contains("configured-client-id")));
    }

    [Fact]
    public async Task ValidateAsync_InvalidJwtException_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.Validator.ValidateAsync(Arg.Any<string>(), Arg.Any<GoogleJsonWebSignature.ValidationSettings>())
            .Returns<GoogleJsonWebSignature.Payload>(_ => throw new InvalidJwtException("bad token"));

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => f.Provider.ValidateAsync("bad-credential", CancellationToken.None));
        Assert.Equal(ApplicationMessages.InvalidExternalCredential, ex.Message);
    }
}
