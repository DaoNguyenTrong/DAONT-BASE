using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Settings;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.Infrastructure.Services.Auth;

internal sealed class RefreshTokenCleanupJob(
    AppDbContext context,
    IOptions<RefreshTokenCleanupSettings> options,
    ILogger<RefreshTokenCleanupJob> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        DateTime cutoff = DateTime.UtcNow.AddDays(-options.Value.RetentionDays);

        int deleted = await context.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff ||
                        (t.RevokedAt != null && t.RevokedAt < cutoff))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("Deleted {Count} expired/revoked refresh tokens.", deleted);
    }
}
