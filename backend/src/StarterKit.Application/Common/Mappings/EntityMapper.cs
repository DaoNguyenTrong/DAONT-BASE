using StarterKit.Application.Services.Accounts;
using StarterKit.Application.Services.Notifications;
using Riok.Mapperly.Abstractions;
using StarterKit.Domain.Entities;

namespace StarterKit.Application.Common.Mappings;

[Mapper]
public static partial class EntityMapper
{
    [MapperIgnoreSource(nameof(Account.CreatedBy))]
    [MapperIgnoreSource(nameof(Account.UpdatedBy))]
    [MapperIgnoreSource(nameof(Account.PasswordHash))]
    public static partial AccountDto ToDto(Account account);

    [MapperIgnoreSource(nameof(Notification.AccountId))]
    [MapperIgnoreSource(nameof(Notification.ReadAt))]
    [MapperIgnoreSource(nameof(Notification.UpdatedAt))]
    [MapperIgnoreSource(nameof(Notification.CreatedBy))]
    [MapperIgnoreSource(nameof(Notification.UpdatedBy))]
    public static partial NotificationDto ToDto(Notification notification);

    [MapperIgnoreSource(nameof(Account.CreatedAt))]
    [MapperIgnoreSource(nameof(Account.UpdatedAt))]
    [MapperIgnoreSource(nameof(Account.CreatedBy))]
    [MapperIgnoreSource(nameof(Account.UpdatedBy))]
    [MapperIgnoreSource(nameof(Account.Status))]
    [MapProperty(nameof(Account.PasswordHash), nameof(ProfileDto.HasPassword))]
    public static partial ProfileDto ToProfileDto(Account account);

    private static bool MapPasswordHashToHasPassword(string? passwordHash) =>
        !string.IsNullOrWhiteSpace(passwordHash);
}
