using System.Linq.Expressions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.Notifications;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Notifications;

public class PushSubscriptionServiceTests
{
    private sealed record Fixture(
        PushSubscriptionService Service,
        IRepository<PushSubscription, Guid> SubscriptionRepo,
        IUnitOfWork UnitOfWork,
        ICurrentUserService CurrentUserService);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<PushSubscription, Guid> subscriptionRepo = Substitute.For<IRepository<PushSubscription, Guid>>();
        unitOfWork.Repository<PushSubscription, Guid>().Returns(subscriptionRepo);

        ICurrentUserService currentUserService = Substitute.For<ICurrentUserService>();

        PushSubscriptionService service = new(unitOfWork, currentUserService);

        return new Fixture(service, subscriptionRepo, unitOfWork, currentUserService);
    }

    // RegisterAsync

    [Fact]
    public async Task RegisterAsync_NewToken_CreatesSubscription()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        f.SubscriptionRepo.FirstOrDefaultAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((PushSubscription?)null);

        await f.Service.RegisterAsync(new RegisterPushSubscriptionRequest("token-1", "Web"), CancellationToken.None);

        await f.SubscriptionRepo.Received(1).AddAsync(
            Arg.Is<PushSubscription>(s => s != null && s.AccountId == accountId && s.Token == "token-1"), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ExistingTokenDifferentAccount_Reassigns()
    {
        Fixture f = CreateFixture();
        Guid previousAccountId = Guid.NewGuid();
        Guid newAccountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(newAccountId.ToString());
        PushSubscription existing = PushSubscription.Create(new PushSubscriptionParams(previousAccountId, "token-1", "Web"));
        f.SubscriptionRepo.FirstOrDefaultAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        await f.Service.RegisterAsync(new RegisterPushSubscriptionRequest("token-1", "Web"), CancellationToken.None);

        Assert.Equal(newAccountId, existing.AccountId);
        f.SubscriptionRepo.Received(1).Update(existing);
        await f.SubscriptionRepo.DidNotReceive().AddAsync(Arg.Any<PushSubscription>(), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ExistingTokenSameAccount_IsIdempotent()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        PushSubscription existing = PushSubscription.Create(new PushSubscriptionParams(accountId, "token-1", "Web"));
        f.SubscriptionRepo.FirstOrDefaultAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        await f.Service.RegisterAsync(new RegisterPushSubscriptionRequest("token-1", "Web"), CancellationToken.None);

        f.SubscriptionRepo.DidNotReceive().Update(Arg.Any<PushSubscription>());
        await f.SubscriptionRepo.DidNotReceive().AddAsync(Arg.Any<PushSubscription>(), Arg.Any<CancellationToken>());
    }

    // RemoveAsync

    [Fact]
    public async Task RemoveAsync_OwnedSubscription_Deletes()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        PushSubscription existing = PushSubscription.Create(new PushSubscriptionParams(accountId, "token-1", "Web"));
        f.SubscriptionRepo.FirstOrDefaultAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        await f.Service.RemoveAsync("token-1", CancellationToken.None);

        f.SubscriptionRepo.Received(1).Delete(existing);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_NotOwnedOrMissing_IsNoOp()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns(Guid.NewGuid().ToString());
        f.SubscriptionRepo.FirstOrDefaultAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((PushSubscription?)null);

        await f.Service.RemoveAsync("token-1", CancellationToken.None);

        f.SubscriptionRepo.DidNotReceive().Delete(Arg.Any<PushSubscription>());
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // GetStatusAsync

    [Fact]
    public async Task GetStatusAsync_HasActiveSubscription_ReturnsTrue()
    {
        Fixture f = CreateFixture();
        Guid accountId = Guid.NewGuid();
        f.CurrentUserService.UserId.Returns(accountId.ToString());
        PushSubscription existing = PushSubscription.Create(new PushSubscriptionParams(accountId, "token-1", "Web"));
        f.SubscriptionRepo.FirstOrDefaultAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        PushSubscriptionStatusResponse result = await f.Service.GetStatusAsync(CancellationToken.None);

        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetStatusAsync_NoActiveSubscription_ReturnsFalse()
    {
        Fixture f = CreateFixture();
        f.CurrentUserService.UserId.Returns(Guid.NewGuid().ToString());
        f.SubscriptionRepo.FirstOrDefaultAsync(
            Arg.Any<Expression<Func<PushSubscription, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((PushSubscription?)null);

        PushSubscriptionStatusResponse result = await f.Service.GetStatusAsync(CancellationToken.None);

        Assert.False(result.IsActive);
    }
}
