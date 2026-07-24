using FeedbackHub.Application.Services.Accounts;
using FeedbackHub.Application.Services.Tenants;
using Riok.Mapperly.Abstractions;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Common.Mappings;

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

    public static TenantDto ToDto(Tenant tenant, TenantRole role) =>
        new(tenant.Id, tenant.Name, tenant.Description, role, tenant.CreatedAt, tenant.UpdatedAt);

    public static TenantParams ToParams(this CreateTenantRequest request) =>
        new(request.Name, request.Description);

    public static TenantMembershipParams ToParams(this InviteMemberRequest request) =>
        new(Guid.Empty, request.AccountId, TenantRole.Member);  // TenantId set by caller

    public static TenantMembershipParams ToParams(this TransferOwnershipRequest request) =>
        new(Guid.Empty, request.NewOwnerId, TenantRole.Owner);  // TenantId set by caller
}
