namespace StarterKit.Domain.Entities;

/// <summary>
/// Marks an entity as scoped to a single organization. <see cref="AppDbContext"/>'s audit
/// interceptor stamps <c>AuditLog.OrganizationId</c> from this for any tracked entity that
/// implements it, so an org-scoped resource's history stays attributable even after the row
/// itself is deleted.
/// </summary>
public interface IHasOrganization
{
    Guid OrganizationId { get; }
}
