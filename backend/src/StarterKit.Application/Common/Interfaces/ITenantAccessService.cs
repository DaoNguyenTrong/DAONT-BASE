namespace StarterKit.Application.Common.Interfaces;

public interface ITenantAccessService
{
    Task<bool> HasActiveAccessAsync(
        Guid accountId, Guid organizationId, CancellationToken cancellationToken = default);

    Task InvalidateMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default);

    Task InvalidateOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default);
}
