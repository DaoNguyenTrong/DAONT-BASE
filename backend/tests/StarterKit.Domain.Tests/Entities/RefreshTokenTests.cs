using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class RefreshTokenTests
{
    private static RefreshTokenParams ValidParams()
    {
        return new RefreshTokenParams(
            AccountId: Guid.NewGuid(),
            TokenHash: "refresh-token-hash-value",
            ExpiresAt: DateTime.UtcNow.AddDays(1),
            DeviceInfo: null,
            IpAddress: null,
            IsPersistent: false,
            LoginAt: DateTime.UtcNow);
    }

    [Fact]
    public void Create_WithValidParams_AssignsAllFields()
    {
        RefreshTokenParams p = ValidParams();

        RefreshToken token = RefreshToken.Create(p);

        Assert.Equal(p.AccountId, token.AccountId);
        Assert.Equal(p.TokenHash, token.TokenHash);
        Assert.Equal(p.ExpiresAt, token.ExpiresAt);
        Assert.Null(token.DeviceInfo);
        Assert.Null(token.IpAddress);
        Assert.False(token.IsPersistent);
        Assert.Equal(p.LoginAt, token.LoginAt);
        Assert.Null(token.RevokedAt);
        Assert.Equal(0, token.Id);
    }

    [Fact]
    public void Create_WithEmptyAccountId_ThrowsDomainException()
    {
        RefreshTokenParams p = ValidParams() with { AccountId = Guid.Empty };

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountIdRequired, () => RefreshToken.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankToken_ThrowsDomainException(string token)
    {
        RefreshTokenParams p = ValidParams() with { TokenHash = token };

        DomainAssert.ThrowsWithMessage(DomainMessages.RefreshTokenRequired, () => RefreshToken.Create(p));
    }

    [Fact]
    public void Create_WithPastExpiresAt_ThrowsDomainException()
    {
        RefreshTokenParams p = ValidParams() with { ExpiresAt = DateTime.UtcNow.AddDays(-1) };

        DomainAssert.ThrowsWithMessage(DomainMessages.RefreshTokenExpiryFuture, () => RefreshToken.Create(p));
    }

    [Fact]
    public void Create_TrimsTokenAndOptionalFields()
    {
        RefreshTokenParams p = ValidParams() with { TokenHash = " token ", DeviceInfo = " device ", IpAddress = " 1.1.1.1 " };

        RefreshToken token = RefreshToken.Create(p);

        Assert.Equal("token", token.TokenHash);
        Assert.Equal("device", token.DeviceInfo);
        Assert.Equal("1.1.1.1", token.IpAddress);
    }

    [Fact]
    public void IsActive_OnFreshToken_IsTrue()
    {
        RefreshToken token = RefreshToken.Create(ValidParams());

        Assert.True(token.IsActive);
    }

    [Fact]
    public void IsActive_AfterRevoke_IsFalseRegardlessOfExpiry()
    {
        RefreshToken token = RefreshToken.Create(ValidParams());

        token.Revoke();

        Assert.False(token.IsActive);
    }

    [Fact]
    public void Revoke_SetsRevokedAtNonNull()
    {
        RefreshToken token = RefreshToken.Create(ValidParams());

        token.Revoke();

        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public void Revoke_CalledTwice_KeepsRevokedAtNonNullAndDoesNotThrow()
    {
        RefreshToken token = RefreshToken.Create(ValidParams());

        token.Revoke();
        DateTime? firstRevokedAt = token.RevokedAt;
        token.Revoke();

        Assert.NotNull(token.RevokedAt);
        Assert.Equal(firstRevokedAt, token.RevokedAt);
    }
}
