using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class PasswordHasherTests
{
    // Work factor 12 costs ~0.3s per hash; the behaviour tests below don't depend on it.
    private readonly PasswordHasher hasher = new(workFactor: 4);

    [Fact]
    public void Hash_ProductionHasher_UsesWorkFactor12()
    {
        string hash = new PasswordHasher().Hash("any-password");

        Assert.Equal("12", hash.Split('$')[2]);
    }

    [Fact]
    public void Hash_ThenVerify_CorrectPassword_ReturnsTrue()
    {
        string hash = hasher.Hash("correct-password");

        Assert.True(hasher.Verify("correct-password", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        string hash = hasher.Hash("correct-password");

        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        Assert.NotEqual(hasher.Hash("same-password"), hasher.Hash("same-password"));
    }
}
