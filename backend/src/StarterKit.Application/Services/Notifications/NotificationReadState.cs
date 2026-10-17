namespace StarterKit.Application.Services.Notifications;

/// <summary>
/// Read-state filter for the notification list. Replaces a nullable <c>unreadOnly</c> flag, whose null and
/// false both meant "all", so "read only" had no way to be asked for.
/// </summary>
public enum NotificationReadState
{
    All = 0,

    Unread = 1,

    Read = 2
}
