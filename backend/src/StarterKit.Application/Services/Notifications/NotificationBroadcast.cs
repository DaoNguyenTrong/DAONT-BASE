namespace StarterKit.Application.Services.Notifications;

/// <summary>
/// One event to tell several accounts about: same type, same payload, same natural key — only the
/// recipients differ. Calling <c>NotifyAsync</c> in a loop instead costs one save and one background job
/// per recipient.
/// </summary>
public sealed record NotificationBroadcast(
    IReadOnlyCollection<Guid> AccountIds,
    string Type,
    string? Data = null,
    string? DedupeKey = null);
