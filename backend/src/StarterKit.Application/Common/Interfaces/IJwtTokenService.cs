using StarterKit.Domain.Entities;

namespace StarterKit.Application.Common.Interfaces;

public interface IJwtTokenService
{
    const string OrganizationIdClaimType = "org_id";

    // Not "sid": the JWT handler maps "sid" to ClaimTypes.Sid, so it would not be read back under its own name.
    const string SessionIdClaimType = "session_id";

    /// <param name="sessionId">The login session (refresh-token family) the token belongs to.</param>
    string GenerateAccessToken(Account account, Guid? organizationId, Guid sessionId);

    string GenerateRefreshToken();
}
