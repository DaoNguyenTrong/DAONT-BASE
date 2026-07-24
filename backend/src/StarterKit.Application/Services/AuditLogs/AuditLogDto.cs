namespace StarterKit.Application.Services.AuditLogs;

public sealed record AuditLogDto(
    long Id,
    string EntityName,
    string EntityId,
    string Action,
    string? OldValues,
    string? NewValues,
    string? UserId,
    string? UserName,
    string? IpAddress,
    string? UserAgent,
    DateTime Timestamp);
