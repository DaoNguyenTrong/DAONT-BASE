using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests.Entities;

public class AccountTests
{
    private static AccountParams ValidParams()
    {
        return new AccountParams(
            Name: "Nguyen Van A",
            Username: "nva",
            Email: "nva@example.com",
            Phone: null,
            Position: null,
            Address: null,
            Status: true);
    }

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        AccountParams p = ValidParams();

        Account account = Account.Create(p);

        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(7, account.Id.Version);
        Assert.Equal(p.Name, account.Name);
        Assert.Equal(p.Username, account.Username);
        Assert.Equal(p.Email, account.Email);
        Assert.Null(account.Phone);
        Assert.Null(account.Position);
        Assert.Null(account.Address);
        Assert.True(account.Status);
        Assert.Null(account.PasswordHash);
        Assert.False(account.EmailConfirmed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string name)
    {
        AccountParams p = ValidParams() with { Name = name };

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountNameRequired, () => Account.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankUsername_ThrowsDomainException(string username)
    {
        AccountParams p = ValidParams() with { Username = username };

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountUsernameRequired, () => Account.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankEmail_ThrowsDomainException(string email)
    {
        AccountParams p = ValidParams() with { Email = email };

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountEmailRequired, () => Account.Create(p));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankOptionalPhone_SetsNull(string? phone)
    {
        AccountParams p = ValidParams() with { Phone = phone };

        Account account = Account.Create(p);

        Assert.Null(account.Phone);
    }

    [Fact]
    public void Create_TrimsOptionalFields()
    {
        AccountParams p = ValidParams() with { Phone = " 0900000000 ", Position = " Engineer ", Address = " 123 Street " };

        Account account = Account.Create(p);

        Assert.Equal("0900000000", account.Phone);
        Assert.Equal("Engineer", account.Position);
        Assert.Equal("123 Street", account.Address);
    }

    [Fact]
    public void Update_ValidatesSameRulesAsCreate()
    {
        Account account = Account.Create(ValidParams());

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountEmailRequired, () => account.Update(ValidParams() with { Email = "" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_WithBlankHash_ThrowsDomainException(string hash)
    {
        Account account = Account.Create(ValidParams());

        DomainAssert.ThrowsWithMessage(DomainMessages.PasswordHashRequired, () => account.SetPasswordHash(hash));
    }

    [Fact]
    public void SetPasswordHash_WithValidHash_TrimsAndAssigns()
    {
        Account account = Account.Create(ValidParams());

        account.SetPasswordHash(" hashed-value ");

        Assert.Equal("hashed-value", account.PasswordHash);
    }

    [Fact]
    public void Update_DoesNotAffectPasswordHash()
    {
        Account account = Account.Create(ValidParams());
        account.SetPasswordHash("hashed-value");

        account.Update(ValidParams());

        Assert.Equal("hashed-value", account.PasswordHash);
    }

    [Fact]
    public void ConfirmEmail_SetsEmailConfirmedTrue()
    {
        Account account = Account.Create(ValidParams());

        account.ConfirmEmail();

        Assert.True(account.EmailConfirmed);
    }

    [Fact]
    public void Update_DoesNotAffectEmailConfirmed()
    {
        Account account = Account.Create(ValidParams());
        account.ConfirmEmail();

        account.Update(ValidParams());

        Assert.True(account.EmailConfirmed);
    }
}
