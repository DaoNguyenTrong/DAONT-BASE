using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class ExternalLoginTests
{
    private static ExternalLoginParams ValidParams()
    {
        return new ExternalLoginParams(
            AccountId: Guid.NewGuid(),
            Provider: "Google",
            ProviderUserId: "google-sub-123",
            Email: "nva@example.com");
    }

    [Fact]
    public void Create_WithValidParams_AssignsAllFields()
    {
        ExternalLoginParams p = ValidParams();

        ExternalLogin login = ExternalLogin.Create(p);

        Assert.Equal(p.AccountId, login.AccountId);
        Assert.Equal(p.Provider, login.Provider);
        Assert.Equal(p.ProviderUserId, login.ProviderUserId);
        Assert.Equal(p.Email, login.Email);
        Assert.NotEqual(Guid.Empty, login.Id);
    }

    [Fact]
    public void Create_WithEmptyAccountId_ThrowsDomainException()
    {
        ExternalLoginParams p = ValidParams() with { AccountId = Guid.Empty };

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountIdRequired, () => ExternalLogin.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankProvider_ThrowsDomainException(string provider)
    {
        ExternalLoginParams p = ValidParams() with { Provider = provider };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.ExternalLoginProviderRequired, () => ExternalLogin.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankProviderUserId_ThrowsDomainException(string providerUserId)
    {
        ExternalLoginParams p = ValidParams() with { ProviderUserId = providerUserId };

        DomainAssert.ThrowsWithMessage(
            DomainMessages.ExternalLoginProviderUserIdRequired, () => ExternalLogin.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankEmail_ThrowsDomainException(string email)
    {
        ExternalLoginParams p = ValidParams() with { Email = email };

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountEmailRequired, () => ExternalLogin.Create(p));
    }

    [Fact]
    public void Create_TrimsProviderAndProviderUserIdAndEmail()
    {
        ExternalLoginParams p = ValidParams() with
        {
            Provider = " Google ",
            ProviderUserId = " google-sub-123 ",
            Email = " nva@example.com "
        };

        ExternalLogin login = ExternalLogin.Create(p);

        Assert.Equal("Google", login.Provider);
        Assert.Equal("google-sub-123", login.ProviderUserId);
        Assert.Equal("nva@example.com", login.Email);
    }
}
