using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.Auth;

public sealed class SessionService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService) : ISessionService
{
    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(
        string? currentRefreshToken,
        CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        string? currentTokenHash = string.IsNullOrWhiteSpace(currentRefreshToken)
            ? null
            : TokenHash.Compute(currentRefreshToken);

        IReadOnlyList<RefreshToken> tokens = await unitOfWork.Repository<RefreshToken, long>()
            .ListAsync(token => token.AccountId == accountId, cancellationToken);

        return tokens
            .Where(token => token.IsActive)
            .OrderByDescending(token => token.CreatedAt)
            .Select(token => new SessionDto(
                token.Id,
                token.DeviceInfo,
                token.IpAddress,
                token.IsPersistent,
                currentTokenHash is not null && token.TokenHash == currentTokenHash,
                token.LoginAt,
                token.CreatedAt,
                token.ExpiresAt))
            .ToList();
    }

    public async Task RevokeSessionAsync(long sessionId, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        IRepository<RefreshToken, long> repository = unitOfWork.Repository<RefreshToken, long>();

        RefreshToken? token = await repository.GetByIdAsync(sessionId, cancellationToken);

        if (token is null || token.AccountId != accountId)
        {
            throw new NotFoundException(nameof(RefreshToken), sessionId);
        }

        token.Revoke();
        repository.Update(token);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeOtherSessionsAsync(string? currentRefreshToken, CancellationToken cancellationToken)
    {
        Guid accountId = GetCurrentAccountId();
        string? currentTokenHash = string.IsNullOrWhiteSpace(currentRefreshToken)
            ? null
            : TokenHash.Compute(currentRefreshToken);

        IRepository<RefreshToken, long> repository = unitOfWork.Repository<RefreshToken, long>();
        IReadOnlyList<RefreshToken> tokens = await repository
            .ListAsync(token => token.AccountId == accountId, cancellationToken);

        foreach (RefreshToken token in tokens)
        {
            if (token.IsActive && token.TokenHash != currentTokenHash)
            {
                token.Revoke();
                repository.Update(token);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Guid GetCurrentAccountId()
    {
        if (!Guid.TryParse(currentUserService.UserId, out Guid accountId))
        {
            throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
        }

        return accountId;
    }
}
