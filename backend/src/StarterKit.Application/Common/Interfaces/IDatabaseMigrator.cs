namespace StarterKit.Application.Common.Interfaces;

/// <summary>Applies pending EF Core migrations — at startup outside Development, and from the <c>migrate</c> command.</summary>
public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken);
}
