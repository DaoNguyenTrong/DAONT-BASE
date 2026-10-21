using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Services.Accounts;
using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Auth;

internal sealed class TokenIssuer(
    IUnitOfWork unitOfWork,
    IJwtTokenService jwtTokenService,
    IPermissionResolver permissionResolver,
    IOptions<JwtSettings> jwtOptions,
    IOptions<StorageSettings> storageOptions) : ITokenIssuer
{
    private readonly JwtSettings jwtSettings = jwtOptions.Value;
    private readonly StorageSettings storageSettings = storageOptions.Value;

    public async Task<Guid?> ResolveDefaultOrganizationIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        IReadOnlyList<OrganizationMember> memberships = await unitOfWork.Repository<OrganizationMember, Guid>()
            .ListAsync(m => m.AccountId == accountId && m.IsActive, cancellationToken);

        return memberships.Count == 1 ? memberships[0].OrganizationId : null;
    }

    public async Task<AuthResult> IssueTokensAsync(
        Account account,
        Guid? organizationId,
        string? deviceInfo,
        string? ipAddress,
        bool isPersistent,
        DateTime loginAt,
        Guid? familyId,
        CancellationToken cancellationToken)
    {
        // A rotated token stays in its login's family, so the family is the session; a new login starts a new one.
        Guid sessionId = familyId is { } existingFamily && existingFamily != Guid.Empty ? existingFamily : Guid.NewGuid();
        string accessToken = jwtTokenService.GenerateAccessToken(account, organizationId, sessionId);
        string refreshToken = jwtTokenService.GenerateRefreshToken();
        DateTime accessTokenExpiry = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenExpiryMinutes);
        DateTime refreshTokenExpiry = DateTime.UtcNow.AddDays(jwtSettings.RefreshTokenExpiryDays);

        RefreshToken token = RefreshToken.Create(new RefreshTokenParams(
            account.Id,
            TokenHash.Compute(refreshToken),
            refreshTokenExpiry,
            deviceInfo,
            ipAddress,
            isPersistent,
            loginAt,
            organizationId,
            sessionId));

        await unitOfWork.Repository<RefreshToken, long>().AddAsync(token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        string? organizationName = organizationId is { } orgId
            ? (await unitOfWork.Repository<Organization, Guid>().GetByIdAsync(orgId, cancellationToken))?.Name
            : null;

        IReadOnlyList<string> permissions = organizationId is { } permissionOrgId
            ? (await permissionResolver.GetEffectivePermissionsAsync(permissionOrgId, account.Id, cancellationToken)).ToList()
            : [];

        return new AuthResult(
            accessToken,
            refreshToken,
            accessTokenExpiry,
            AccountMapper.ToDto(account) with
            {
                AvatarUrl = account.AvatarPath is null ? null : storageSettings.BuildPublicUrl(account.AvatarPath),
            },
            isPersistent,
            organizationId,
            organizationName,
            permissions);
    }
}
