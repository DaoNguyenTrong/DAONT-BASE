using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Infrastructure.Persistence;

namespace FeedbackHub.Infrastructure.Services;

internal sealed class RefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshTokenCleanupSettings> options,
    ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromHours(options.Value.IntervalHours));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-options.Value.RetentionDays);
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            int deleted = await context.RefreshTokens
                .Where(t => t.ExpiresAt < cutoff ||
                            (t.RevokedAt != null && t.RevokedAt < cutoff))
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
                logger.LogInformation("Deleted {Count} expired/revoked refresh tokens.", deleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Refresh token cleanup failed.");
        }
    }
}
