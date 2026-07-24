using StarterKit.Application.Services.Accounts;
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

    public static AccountParams ToParams(this CreateAccountRequest request) =>
        new(
            request.Name,
            request.Username,
            request.Email,
            request.Phone,
            request.Position,
            request.Address,
            request.Status);

    public static AccountParams ToParams(this UpdateAccountRequest request) =>
        new(
            request.Name,
            request.Username,
            request.Email,
            request.Phone,
            request.Position,
            request.Address,
            request.Status);

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
