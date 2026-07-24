using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record ExternalLoginParams(
    Guid AccountId,
    string Provider,
    string ProviderUserId,
    string Email);

public sealed class ExternalLogin : BaseEntity<Guid>
{
    private ExternalLogin()
    {
    }

    public static ExternalLogin Create(ExternalLoginParams p)
    {
        if (p.AccountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Provider))
        {
            throw new DomainException(DomainMessages.ExternalLoginProviderRequired);
        }

        if (string.IsNullOrWhiteSpace(p.ProviderUserId))
        {
            throw new DomainException(DomainMessages.ExternalLoginProviderUserIdRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Email))
        {
            throw new DomainException(DomainMessages.AccountEmailRequired);
        }

        return new ExternalLogin
        {
            Id = IdGenerator.NewUuidV7(),
            AccountId = p.AccountId,
            Provider = p.Provider.Trim(),
            ProviderUserId = p.ProviderUserId.Trim(),
            Email = p.Email.Trim()
        };
    }

    public Guid AccountId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ProviderUserId { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;
}
