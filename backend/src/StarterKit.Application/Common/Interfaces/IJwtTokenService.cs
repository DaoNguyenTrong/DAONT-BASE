using StarterKit.Domain.Entities;

namespace StarterKit.Application.Common.Interfaces;

public interface IJwtTokenService
{
    const string OrganizationIdClaimType = "org_id";

    string GenerateAccessToken(Account account, Guid? organizationId);

    string GenerateRefreshToken();
}
