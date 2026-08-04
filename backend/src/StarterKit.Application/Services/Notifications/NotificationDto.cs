namespace StarterKit.Application.Services.Notifications;

public sealed record NotificationDto(Guid Id, string Type, string? Data, bool IsRead, DateTime CreatedAt);
