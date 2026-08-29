using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Auth;

public interface ITokenIssuer
{
    Task<Guid?> ResolveDefaultOrganizationIdAsync(Guid accountId, CancellationToken cancellationToken);

    Task<AuthResult> IssueTokensAsync(
        Account account,
        Guid? organizationId,
        string? deviceInfo,
        string? ipAddress,
        bool isPersistent,
        DateTime loginAt,
        Guid? familyId,
        CancellationToken cancellationToken);
}
