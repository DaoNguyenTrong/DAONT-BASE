using StarterKit.Domain.Entities;

namespace StarterKit.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(Account account);

    string GenerateRefreshToken();
}
