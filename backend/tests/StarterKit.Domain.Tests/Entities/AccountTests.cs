using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

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
            Address: null);
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankOptionalPosition_SetsNull(string? position)
    {
        AccountParams p = ValidParams() with { Position = position };

        Account account = Account.Create(p);

        Assert.Null(account.Position);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankOptionalAddress_SetsNull(string? address)
    {
        AccountParams p = ValidParams() with { Address = address };

        Account account = Account.Create(p);

        Assert.Null(account.Address);
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
    public void Create_TrimsRequiredFields()
    {
        AccountParams p = ValidParams() with { Name = " Nguyen Van A ", Username = " nva ", Email = " nva@example.com " };

        Account account = Account.Create(p);

        Assert.Equal("Nguyen Van A", account.Name);
        Assert.Equal("nva", account.Username);
        Assert.Equal("nva@example.com", account.Email);
    }

    [Fact]
    public void Update_NeverChangesStatus()
    {
        // AccountParams carries no Status: a profile edit can't silently (re)activate an account by
        // forgetting to copy the current value through.
        Account account = Account.Create(ValidParams());

        account.Update(ValidParams() with { Name = "Renamed" });

        Assert.True(account.Status);
    }

    [Fact]
    public void Deactivate_ThenActivate_TogglesStatus()
    {
        Account account = Account.Create(ValidParams());

        account.Deactivate();
        Assert.False(account.Status);

        account.Activate();
        Assert.True(account.Status);
    }

    [Fact]
    public void Create_LowerCasesUsernameAndEmail()
    {
        Account account = Account.Create(ValidParams() with { Username = " NvA ", Email = " NVA@Example.COM " });

        Assert.Equal("nva", account.Username);
        Assert.Equal("nva@example.com", account.Email);
    }

    [Theory]
    [InlineData("Alice", "alice")]
    [InlineData("  BOB  ", "bob")]
    [InlineData("carol", "carol")]
    public void NormalizeUsername_TrimsAndLowerCases(string input, string expected)
    {
        Assert.Equal(expected, Account.NormalizeUsername(input));
    }

    [Theory]
    [InlineData("A@X.com", "a@x.com")]
    [InlineData(" a@x.com ", "a@x.com")]
    public void NormalizeEmail_TrimsAndLowerCases(string input, string expected)
    {
        Assert.Equal(expected, Account.NormalizeEmail(input));
    }

    [Fact]
    public void NormalizeUsername_UsesInvariantCulture()
    {
        // Turkish-I: culture-sensitive lower-casing would turn "I" into a dotless "ı" under tr-TR.
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
        try
        {
            Assert.Equal("ivan", Account.NormalizeUsername("IVAN"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
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

    [Fact]
    public void SetAvatar_StoresTrimmedPath_AndNullClearsIt()
    {
        Account account = Account.Create(ValidParams());

        account.SetAvatar("  public/avatars/a.png ");
        Assert.Equal("public/avatars/a.png", account.AvatarPath);

        account.SetAvatar(null);
        Assert.Null(account.AvatarPath);
    }

    [Fact]
    public void SetAvatar_BlankPath_ClearsIt()
    {
        Account account = Account.Create(ValidParams());
        account.SetAvatar("public/avatars/a.png");

        account.SetAvatar("   ");

        Assert.Null(account.AvatarPath);
    }
}
