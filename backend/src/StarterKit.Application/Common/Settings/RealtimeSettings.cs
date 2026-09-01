namespace StarterKit.Application.Common.Settings;

public sealed class RealtimeSettings
{
    /// <summary>
    /// SignalR scale-out backplane. <c>None</c> (default) keeps the in-memory bus — fine for a
    /// single API instance. <c>Redis</c> requires <c>ConnectionStrings:Redis</c> and must be used
    /// together with <see cref="CacheSettings.Provider"/> = <c>Redis</c> when scaling out.
    /// </summary>
    public string Backplane { get; init; } = "None";
}
