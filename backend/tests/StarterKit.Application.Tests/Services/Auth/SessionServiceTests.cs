using System.Linq.Expressions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Auth;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Auth;

public class SessionServiceTests
{
    private sealed record Fixture(
        SessionService Service,
        IRepository<RefreshToken, long> RefreshTokenRepo,
        IUnitOfWork UnitOfWork,
        ICurrentUserService CurrentUserService);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<RefreshToken, long> refreshTokenRepo = Substitute.For<IRepository<RefreshToken, long>>();
        unitOfWork.Repository<RefreshToken, long>().Returns(refreshTokenRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();

        SessionService service = new(unitOfWork, currentUserService);

        return new Fixture(service, refreshTokenRepo, unitOfWork, currentUserService);
    }

    private static RefreshToken CreateRefreshToken(
        Guid accountId,
        string rawToken,
        bool isPersistent = false,
        DateTime? expiresAt = null,
        DateTime? loginAt = null)
    {
        return RefreshToken.Create(new RefreshTokenParams(
            accountId,
            TokenHash.Compute(rawToken),
            expiresAt ?? DateTime.UtcNow.AddDays(1),
            DeviceInfo: null,
            IpAddress: null,
            IsPersistent: isPersistent,
            LoginAt: loginAt ?? DateTime.UtcNow));
    }

    // GetSessionsAsync

    [Fact]
    public async Task GetSessionsAsync_ExcludesRevokedTokens_MarksCurrentAndOrdersNewestFirst()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        RefreshToken older = CreateRefreshToken(accountId, "older-token");
        older.Id = 1;
        older.CreatedAt = DateTime.UtcNow.AddHours(-1);

        RefreshToken newer = CreateRefreshToken(accountId, "newer-token");
        newer.Id = 2;
        newer.CreatedAt = DateTime.UtcNow;

        RefreshToken revoked = CreateRefreshToken(accountId, "revoked-token");
        revoked.Id = 3;
        revoked.Revoke();

        f.RefreshTokenRepo.ListAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([older, newer, revoked]);

        IReadOnlyList<SessionDto> sessions = await f.Service.GetSessionsAsync("newer-token", CancellationToken.None);

        Assert.Equal(2, sessions.Count);
        Assert.Equal(newer.Id, sessions[0].Id);
        Assert.True(sessions[0].IsCurrent);
        Assert.Equal(older.Id, sessions[1].Id);
        Assert.False(sessions[1].IsCurrent);
    }

    [Fact]
    public async Task GetSessionsAsync_NotAuthenticated_ThrowsUnauthorized()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns((string?)null);

        await ApplicationAssert.ThrowsWithMessageAsync<UnauthorizedException>(
            ApplicationMessages.AuthenticatedUserRequired,
            () => f.Service.GetSessionsAsync(null, CancellationToken.None));
    }

    // RevokeSessionAsync

    [Fact]
    public async Task RevokeSessionAsync_TokenNotFound_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns(Guid.NewGuid().ToString());
        f.RefreshTokenRepo.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        await ApplicationAssert.AssertNotFoundAsync<RefreshToken>(
            42L,
            () => f.Service.RevokeSessionAsync(42, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeSessionAsync_TokenNotOwnedByCurrentAccount_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns(Guid.NewGuid().ToString());
        RefreshToken othersToken = CreateRefreshToken(Guid.NewGuid(), "some-token");
        othersToken.Id = 42;
        f.RefreshTokenRepo.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns(othersToken);

        await ApplicationAssert.AssertNotFoundAsync<RefreshToken>(
            42L,
            () => f.Service.RevokeSessionAsync(42, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeSessionAsync_OwnedByCurrentAccount_RevokesAndSaves()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        RefreshToken token = CreateRefreshToken(accountId, "some-token");
        token.Id = 7;
        f.RefreshTokenRepo.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(token);

        await f.Service.RevokeSessionAsync(7, CancellationToken.None);

        Assert.NotNull(token.RevokedAt);
        f.RefreshTokenRepo.Received(1).Update(token);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // RevokeOtherSessionsAsync

    [Fact]
    public async Task RevokeOtherSessionsAsync_RevokesEveryTokenExceptCurrent()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        RefreshToken current = CreateRefreshToken(accountId, "current-token");
        RefreshToken other1 = CreateRefreshToken(accountId, "other-token-1");
        RefreshToken other2 = CreateRefreshToken(accountId, "other-token-2");
        RefreshToken alreadyRevoked = CreateRefreshToken(accountId, "already-revoked");
        alreadyRevoked.Revoke();

        f.RefreshTokenRepo.ListAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([current, other1, other2, alreadyRevoked]);

        await f.Service.RevokeOtherSessionsAsync("current-token", CancellationToken.None);

        Assert.Null(current.RevokedAt);
        Assert.NotNull(other1.RevokedAt);
        Assert.NotNull(other2.RevokedAt);
        f.RefreshTokenRepo.DidNotReceive().Update(current);
        f.RefreshTokenRepo.Received(1).Update(other1);
        f.RefreshTokenRepo.Received(1).Update(other2);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeOtherSessionsAsync_NoCurrentTokenIdentified_RevokesAllActiveSessions()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());

        RefreshToken token1 = CreateRefreshToken(accountId, "token-1");
        RefreshToken token2 = CreateRefreshToken(accountId, "token-2");

        f.RefreshTokenRepo.ListAsync(Arg.Any<Expression<Func<RefreshToken, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([token1, token2]);

        await f.Service.RevokeOtherSessionsAsync(null, CancellationToken.None);

        Assert.NotNull(token1.RevokedAt);
        Assert.NotNull(token2.RevokedAt);
    }
}
