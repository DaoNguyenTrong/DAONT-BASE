using Riok.Mapperly.Abstractions;
using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Accounts;

[Mapper]
public static partial class AccountMapper
{
    [MapperIgnoreSource(nameof(Account.CreatedBy))]
    [MapperIgnoreSource(nameof(Account.UpdatedBy))]
    [MapperIgnoreSource(nameof(Account.FailedLoginCount))]
    [MapperIgnoreSource(nameof(Account.FailedLoginWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.LockedUntil))]
    [MapperIgnoreSource(nameof(Account.FailedPasswordChangeCount))]
    [MapperIgnoreSource(nameof(Account.PasswordChangeWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.PasswordChangeLockedUntil))]
    [MapperIgnoreSource(nameof(Account.PasswordHash))]
    [MapperIgnoreSource(nameof(Account.AvatarPath))]
    [MapperIgnoreTarget(nameof(AccountDto.AvatarUrl))]
    public static partial AccountDto ToDto(Account account);

    [MapperIgnoreSource(nameof(Account.CreatedAt))]
    [MapperIgnoreSource(nameof(Account.UpdatedAt))]
    [MapperIgnoreSource(nameof(Account.CreatedBy))]
    [MapperIgnoreSource(nameof(Account.UpdatedBy))]
    [MapperIgnoreSource(nameof(Account.FailedLoginCount))]
    [MapperIgnoreSource(nameof(Account.FailedLoginWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.LockedUntil))]
    [MapperIgnoreSource(nameof(Account.FailedPasswordChangeCount))]
    [MapperIgnoreSource(nameof(Account.PasswordChangeWindowStartedAt))]
    [MapperIgnoreSource(nameof(Account.PasswordChangeLockedUntil))]
    [MapperIgnoreSource(nameof(Account.Status))]
    [MapperIgnoreSource(nameof(Account.AvatarPath))]
    [MapperIgnoreTarget(nameof(ProfileDto.AvatarUrl))]
    [MapProperty(nameof(Account.PasswordHash), nameof(ProfileDto.HasPassword))]
    public static partial ProfileDto ToProfileDto(Account account);

    private static bool MapPasswordHashToHasPassword(string? passwordHash) =>
        !string.IsNullOrWhiteSpace(passwordHash);
}
