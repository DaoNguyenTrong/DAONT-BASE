namespace StarterKit.Application.Common.Settings;

public sealed class JwtSettings
{
    public string SecretKey { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string[] Audiences { get; init; } = [];

    public int AccessTokenExpiryMinutes { get; init; } = 15;

    public int RefreshTokenExpiryDays { get; init; } = 7;
}
