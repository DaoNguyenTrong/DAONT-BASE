using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record RefreshTokenParams(
    Guid AccountId,
    string TokenHash,
    DateTime ExpiresAt,
    string? DeviceInfo,
    string? IpAddress,
    bool IsPersistent,
    DateTime LoginAt,
    Guid? OrganizationId = null,
    Guid? FamilyId = null);

public sealed class RefreshToken : BaseEntity<long>
{
    private RefreshToken()
    {
    }

    public static RefreshToken Create(RefreshTokenParams p)
    {
        if (p.AccountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        if (string.IsNullOrWhiteSpace(p.TokenHash))
        {
            throw new DomainException(DomainMessages.RefreshTokenRequired);
        }

        if (p.ExpiresAt <= DateTime.UtcNow)
        {
            throw new DomainException(DomainMessages.RefreshTokenExpiryFuture);
        }

        return new RefreshToken
        {
            AccountId = p.AccountId,
            TokenHash = p.TokenHash.Trim(),
            ExpiresAt = p.ExpiresAt,
            DeviceInfo = string.IsNullOrWhiteSpace(p.DeviceInfo) ? null : p.DeviceInfo.Trim(),
            IpAddress = string.IsNullOrWhiteSpace(p.IpAddress) ? null : p.IpAddress.Trim(),
            IsPersistent = p.IsPersistent,
            LoginAt = p.LoginAt,
            OrganizationId = p.OrganizationId,
            // Every token minted from one login chain shares a FamilyId. A fresh
            // login starts a new family; a rotation inherits the presented token's.
            // The refresh flow uses it to tell a legitimate concurrent-tab rotation
            // apart from a replayed (stolen) token.
            FamilyId = p.FamilyId is { } familyId && familyId != Guid.Empty ? familyId : Guid.NewGuid()
        };
    }

    public Guid AccountId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime ExpiresAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public string? DeviceInfo { get; private set; }

    public string? IpAddress { get; private set; }

    public bool IsPersistent { get; private set; }

    public DateTime LoginAt { get; private set; }

    public Guid? OrganizationId { get; private set; }

    public Guid FamilyId { get; private set; }

    public bool IsActive => !RevokedAt.HasValue && DateTime.UtcNow < ExpiresAt;

    public void Revoke()
    {
        if (!RevokedAt.HasValue)
        {
            RevokedAt = DateTime.UtcNow;
        }
    }
}
