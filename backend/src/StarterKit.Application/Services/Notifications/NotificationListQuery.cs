namespace StarterKit.Application.Services.Notifications;

/// <summary>
/// Query for the caller's own notification list. Its own record rather than <c>PaginationRequest</c>, whose
/// <c>Search</c> the service never read — the contract promised a filter the server silently ignored.
/// </summary>
public sealed record NotificationListQuery(
    int PageNumber = 1,
    int PageSize = 10,
    NotificationReadState ReadState = NotificationReadState.All);
