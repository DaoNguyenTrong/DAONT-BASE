using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_CorrectPassword_ReturnsTrue()
    {
        PasswordHasher hasher = new();

        string hash = hasher.Hash("correct-password");

        Assert.True(hasher.Verify("correct-password", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        PasswordHasher hasher = new();

        string hash = hasher.Hash("correct-password");

        Assert.False(hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        PasswordHasher hasher = new();

        string first = hasher.Hash("same-password");
        string second = hasher.Hash("same-password");

        Assert.NotEqual(first, second);
    }
}
