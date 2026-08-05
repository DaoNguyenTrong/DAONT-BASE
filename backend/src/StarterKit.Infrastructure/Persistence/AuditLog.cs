namespace StarterKit.Infrastructure.Persistence;

public sealed class AuditLog
{
    public long Id { get; set; }

    public required string EntityName { get; set; }

    public required string EntityId { get; set; }

    public required string Action { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? UserId { get; set; }

    public Guid? OrganizationId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; }
}
