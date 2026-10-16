using Microsoft.EntityFrameworkCore;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Persistence;

internal sealed class DatabaseMigrator(AppDbContext dbContext) : IDatabaseMigrator
{
    public Task MigrateAsync(CancellationToken cancellationToken) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}
