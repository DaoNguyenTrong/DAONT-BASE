using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(Account account);

    string GenerateRefreshToken();
}
