using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace StarterKit.Infrastructure.Persistence;

internal sealed class AuditEntry(EntityEntry entry)
{
    public EntityEntry Entry { get; } = entry;

    public required string EntityName { get; init; }

    public required string Action { get; init; }

    public string? UserId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public Dictionary<string, object?> OldValues { get; } = [];

    public Dictionary<string, object?> NewValues { get; } = [];

    public List<PropertyEntry> TemporaryProperties { get; } = [];

    public bool HasTemporaryProperties => TemporaryProperties.Count > 0;

    public AuditLog ToAuditLog(DateTime timestamp)
    {
        var entityId = Entry.Properties
            .FirstOrDefault(p => p.Metadata.Name == "Id")?
            .CurrentValue?.ToString() ?? string.Empty;

        return new AuditLog
        {
            EntityName = EntityName,
            EntityId = entityId,
            Action = Action,
            OldValues = OldValues.Count > 0 ? JsonSerializer.Serialize(OldValues) : null,
            NewValues = NewValues.Count > 0 ? JsonSerializer.Serialize(NewValues) : null,
            UserId = UserId,
            IpAddress = IpAddress,
            UserAgent = UserAgent,
            Timestamp = timestamp
        };
    }
}
