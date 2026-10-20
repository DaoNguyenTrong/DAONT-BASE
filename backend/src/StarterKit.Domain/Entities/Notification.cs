using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record NotificationParams(Guid AccountId, string Type, string? Data = null, string? DedupeKey = null);

public sealed class Notification : BaseEntity<Guid>
{
    public const int DedupeKeyMaxLength = 200;

    private Notification()
    {
    }

    public static Notification Create(NotificationParams p)
    {
        if (p.AccountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Type))
        {
            throw new DomainException(DomainMessages.NotificationTypeRequired);
        }

        string? dedupeKey = string.IsNullOrWhiteSpace(p.DedupeKey) ? null : p.DedupeKey.Trim();
        if (dedupeKey?.Length > DedupeKeyMaxLength)
        {
            throw new DomainException(DomainMessages.NotificationDedupeKeyTooLong);
        }

        return new Notification
        {
            Id = IdGenerator.NewUuidV7(),
            AccountId = p.AccountId,
            Type = p.Type.Trim(),
            Data = p.Data,
            DedupeKey = dedupeKey
        };
    }

    public Guid AccountId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string? Data { get; private set; }

    /// <summary>
    /// Natural key of the producing event, or <c>null</c> if none. Unique per (<see cref="AccountId"/>, key), so a
    /// re-run job or retried webhook can't notify the same person twice. It names the <i>event</i>, not the run:
    /// two runs for the same event must produce the same key.
    /// </summary>
    public string? DedupeKey { get; private set; }

    public DateTime? ReadAt { get; private set; }

    public bool IsRead => ReadAt.HasValue;

    public void MarkRead()
    {
        if (!ReadAt.HasValue)
        {
            ReadAt = DateTime.UtcNow;
        }
    }
}
