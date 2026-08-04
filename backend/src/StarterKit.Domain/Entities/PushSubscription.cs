using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record PushSubscriptionParams(Guid AccountId, string Token, string Platform);

public sealed class PushSubscription : BaseEntity<Guid>
{
    private PushSubscription()
    {
    }

    public static PushSubscription Create(PushSubscriptionParams p)
    {
        if (p.AccountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Token))
        {
            throw new DomainException(DomainMessages.PushSubscriptionTokenRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Platform))
        {
            throw new DomainException(DomainMessages.PushSubscriptionPlatformRequired);
        }

        return new PushSubscription
        {
            Id = IdGenerator.NewUuidV7(),
            AccountId = p.AccountId,
            Token = p.Token.Trim(),
            Platform = p.Platform.Trim()
        };
    }

    public Guid AccountId { get; private set; }

    public string Token { get; private set; } = string.Empty;

    public string Platform { get; private set; } = string.Empty;

    public void ReassignTo(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        AccountId = accountId;
    }
}
