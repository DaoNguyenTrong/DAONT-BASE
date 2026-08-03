namespace StarterKit.Application.Common.Interfaces;

public interface IPermissionResolver
{
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default);

    Task InvalidateMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default);

    Task InvalidateOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default);
}
