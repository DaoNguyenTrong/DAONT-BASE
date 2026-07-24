namespace StarterKit.Application.Services.Auth;

public sealed record SessionDto(
    long Id,
    string? DeviceInfo,
    string? IpAddress,
    bool IsPersistent,
    bool IsCurrent,
    DateTime LoginAt,
    DateTime LastActiveAt,
    DateTime ExpiresAt);
