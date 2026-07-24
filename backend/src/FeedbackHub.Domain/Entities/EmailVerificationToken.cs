using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Entities;

public record EmailVerificationTokenParams(
    Guid AccountId,
    string TokenHash,
    DateTime ExpiresAt);

public sealed class EmailVerificationToken : BaseEntity<Guid>
{
    private EmailVerificationToken()
    {
    }

    public static EmailVerificationToken Create(EmailVerificationTokenParams p)
    {
        if (p.AccountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        if (string.IsNullOrWhiteSpace(p.TokenHash))
        {
            throw new DomainException(DomainMessages.EmailVerificationTokenRequired);
        }

        if (p.ExpiresAt <= DateTime.UtcNow)
        {
            throw new DomainException(DomainMessages.EmailVerificationTokenExpiryFuture);
        }

        return new EmailVerificationToken
        {
            Id = IdGenerator.NewUuidV7(),
            AccountId = p.AccountId,
            TokenHash = p.TokenHash.Trim(),
            ExpiresAt = p.ExpiresAt
        };
    }

    public Guid AccountId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? ConsumedAt { get; private set; }

    public bool IsActive => !ConsumedAt.HasValue && DateTime.UtcNow < ExpiresAt;

    public void Consume()
    {
        if (!ConsumedAt.HasValue)
        {
            ConsumedAt = DateTime.UtcNow;
        }
    }
}
