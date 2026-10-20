namespace StarterKit.Domain.Entities;

/// <summary>
/// Marks an entity as scoped to one organization: <see cref="AppDbContext"/>'s audit interceptor stamps
/// <c>AuditLog.OrganizationId</c> from it, so the resource's history stays attributable after the row is deleted.
/// </summary>
public interface IHasOrganization
{
    Guid OrganizationId { get; }
}
