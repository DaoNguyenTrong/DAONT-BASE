namespace FeedbackHub.Application.Common.Settings;

public sealed class RefreshTokenCleanupSettings
{
    public int IntervalHours { get; init; } = 24;
    public int RetentionDays { get; init; } = 7;
}
