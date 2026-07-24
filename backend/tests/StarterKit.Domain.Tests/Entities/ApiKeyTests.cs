using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class ApiKeyTests
{
    private static ApiKeyParams ValidParams()
    {
        return new ApiKeyParams(Name: "Production key");
    }

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        ApiKey key = ApiKey.Create(ValidParams(), "prefix_abc", "hash_xyz");

        Assert.NotEqual(Guid.Empty, key.Id);
        Assert.Equal("Production key", key.Name);
        Assert.Equal("prefix_abc", key.KeyPrefix);
        Assert.Equal("hash_xyz", key.KeyHash);
        Assert.True(key.IsActive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string name)
    {
        ApiKeyParams p = ValidParams() with { Name = name };

        DomainAssert.ThrowsWithMessage(DomainMessages.ApiKeyNameRequired, () => ApiKey.Create(p, "prefix", "hash"));
    }

    [Fact]
    public void Create_WithEmptyKeyPrefixOrKeyHash_DoesNotThrow()
    {
        // KeyPrefix/KeyHash are unvalidated at the domain layer — empty strings pass through silently.
        ApiKey key = ApiKey.Create(ValidParams(), "", "");

        Assert.Equal(string.Empty, key.KeyPrefix);
        Assert.Equal(string.Empty, key.KeyHash);
    }

    [Fact]
    public void Create_TrimsKeyPrefixAndKeyHash()
    {
        ApiKey key = ApiKey.Create(ValidParams(), "  prefix  ", "  hash  ");

        Assert.Equal("prefix", key.KeyPrefix);
        Assert.Equal("hash", key.KeyHash);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        ApiKey key = ApiKey.Create(ValidParams(), "prefix", "hash");

        key.Deactivate();

        Assert.False(key.IsActive);
    }

    [Fact]
    public void Deactivate_CalledTwice_StaysFalseAndDoesNotThrow()
    {
        ApiKey key = ApiKey.Create(ValidParams(), "prefix", "hash");

        key.Deactivate();
        key.Deactivate();

        Assert.False(key.IsActive);
    }
}
