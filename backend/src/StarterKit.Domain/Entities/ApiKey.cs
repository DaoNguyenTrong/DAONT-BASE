using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record ApiKeyParams(string Name);

public sealed class ApiKey : BaseEntity<Guid>
{
    private ApiKey()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string KeyPrefix { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public static ApiKey Create(ApiKeyParams p, string keyPrefix, string keyHash)
    {
        ApiKey key = new() { Id = IdGenerator.NewUuidV7() };
        key.Update(p);
        key.KeyPrefix = keyPrefix.Trim();
        key.KeyHash = keyHash.Trim();
        return key;
    }

    public void Update(ApiKeyParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
        {
            throw new DomainException(DomainMessages.ApiKeyNameRequired);
        }

        Name = p.Name.Trim();
    }

    public void Deactivate() => IsActive = false;
}
