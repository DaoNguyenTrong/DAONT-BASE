namespace StarterKit.Application.Common.Settings;

public sealed class DatabaseSettings
{
    /// <summary>
    /// Apply pending migrations when the API starts outside Development. Instances starting together would each
    /// try the same migration, so a multi-instance deploy sets this <c>false</c> and runs <c>migrate</c> once first.
    /// </summary>
    public bool MigrateOnStartup { get; init; } = true;
}
