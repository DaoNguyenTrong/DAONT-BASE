using Riok.Mapperly.Abstractions;
using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Notifications;

[Mapper]
public static partial class NotificationMapper
{
    [MapperIgnoreSource(nameof(Notification.AccountId))]
    [MapperIgnoreSource(nameof(Notification.ReadAt))]
    [MapperIgnoreSource(nameof(Notification.UpdatedAt))]
    [MapperIgnoreSource(nameof(Notification.CreatedBy))]
    [MapperIgnoreSource(nameof(Notification.UpdatedBy))]
    [MapperIgnoreSource(nameof(Notification.DedupeKey))]
    public static partial NotificationDto ToDto(Notification notification);
}
