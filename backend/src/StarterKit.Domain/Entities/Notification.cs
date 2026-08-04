using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record NotificationParams(Guid AccountId, string Type, string? Data = null);

public sealed class Notification : BaseEntity<Guid>
{
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

        return new Notification
        {
            Id = IdGenerator.NewUuidV7(),
            AccountId = p.AccountId,
            Type = p.Type.Trim(),
            Data = p.Data
        };
    }

    public Guid AccountId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string? Data { get; private set; }

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
