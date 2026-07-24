namespace FeedbackHub.Application.Common.Settings;

public sealed class EmailSettings
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = string.Empty;

    /// <summary>Implicit TLS on connect (port 465). Leave false for STARTTLS (port 587, the default here).</summary>
    public bool UseSsl { get; init; } = false;

    public int VerificationTokenExpiryHours { get; init; } = 24;

    public string FrontendBaseUrl { get; init; } = string.Empty;
}
