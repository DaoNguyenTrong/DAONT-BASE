using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Services.Auth;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class RefreshTokenCleanupJobTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private Account account = null!;

    public async Task InitializeAsync()
    {
        account = Account.Create(new AccountParams("Cleanup Target", "cleanup-target", "cleanup-target@example.com"));
        await using AppDbContext context = fixture.CreateDbContext();
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        context.Accounts.Remove(await context.Accounts.SingleAsync(a => a.Id == account.Id));
        await context.SaveChangesAsync();
    }

    private RefreshTokenCleanupJob CreateJob(AppDbContext context, int retentionDays = 7)
    {
        IOptions<RefreshTokenCleanupSettings> options = Options.Create(new RefreshTokenCleanupSettings
        {
            RetentionDays = retentionDays
        });

        return new RefreshTokenCleanupJob(context, options, NullLogger<RefreshTokenCleanupJob>.Instance);
    }

    private static RefreshToken CreateToken(Guid accountId, string rawToken)
    {
        return RefreshToken.Create(new RefreshTokenParams(
            accountId,
            "hash-" + rawToken,
            DateTime.UtcNow.AddDays(1),
            DeviceInfo: null,
            IpAddress: null,
            IsPersistent: false,
            LoginAt: DateTime.UtcNow));
    }

    private static void ForceExpiresAt(RefreshToken token, DateTime expiresAt) =>
        typeof(RefreshToken).GetProperty(nameof(RefreshToken.ExpiresAt))!.SetValue(token, expiresAt);

    private static void ForceRevokedAt(RefreshToken token, DateTime revokedAt) =>
        typeof(RefreshToken).GetProperty(nameof(RefreshToken.RevokedAt))!.SetValue(token, revokedAt);

    [Fact]
    public async Task RunAsync_DeletesExpiredAndOldRevoked_KeepsValidAndRecentlyRevoked()
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-7);

        RefreshToken expired = CreateToken(account.Id, "expired");
        ForceExpiresAt(expired, cutoff.AddDays(-1));

        RefreshToken oldRevoked = CreateToken(account.Id, "old-revoked");
        ForceRevokedAt(oldRevoked, cutoff.AddDays(-1));

        RefreshToken stillValid = CreateToken(account.Id, "still-valid");

        RefreshToken recentlyRevoked = CreateToken(account.Id, "recently-revoked");
        recentlyRevoked.Revoke();

        await using (AppDbContext seedContext = fixture.CreateDbContext())
        {
            seedContext.RefreshTokens.AddRange(expired, oldRevoked, stillValid, recentlyRevoked);
            await seedContext.SaveChangesAsync();
        }

        await using (AppDbContext jobContext = fixture.CreateDbContext())
        {
            RefreshTokenCleanupJob job = CreateJob(jobContext, retentionDays: 7);
            await job.RunAsync(CancellationToken.None);
        }

        await using AppDbContext verifyContext = fixture.CreateDbContext();
        List<string> remainingTokenHashes = await verifyContext.RefreshTokens
            .Where(t => t.AccountId == account.Id)
            .Select(t => t.TokenHash)
            .ToListAsync();

        Assert.DoesNotContain("hash-expired", remainingTokenHashes);
        Assert.DoesNotContain("hash-old-revoked", remainingTokenHashes);
        Assert.Contains("hash-still-valid", remainingTokenHashes);
        Assert.Contains("hash-recently-revoked", remainingTokenHashes);
    }

    [Fact]
    public async Task RunAsync_NothingToDelete_DoesNotThrow()
    {
        RefreshToken stillValid = CreateToken(account.Id, "nothing-to-delete");
        await using (AppDbContext seedContext = fixture.CreateDbContext())
        {
            seedContext.RefreshTokens.Add(stillValid);
            await seedContext.SaveChangesAsync();
        }

        await using AppDbContext jobContext = fixture.CreateDbContext();
        RefreshTokenCleanupJob job = CreateJob(jobContext);

        await job.RunAsync(CancellationToken.None);
    }
}
