namespace StarterKit.Application.Common.Settings;

public sealed class DatabaseSettings
{
    /// <summary>
    /// Apply pending migrations when the API starts outside Development (default <c>true</c>, right for a
    /// single instance). Several instances starting together would each try the same migration, so a
    /// multi-instance deploy sets this to <c>false</c> and runs the <c>migrate</c> command once before
    /// starting them.
    /// </summary>
    public bool MigrateOnStartup { get; init; } = true;
}
