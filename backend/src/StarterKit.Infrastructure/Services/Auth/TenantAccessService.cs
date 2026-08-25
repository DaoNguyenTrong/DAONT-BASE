using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Services.Auth;

internal sealed class TenantAccessService(
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    IOptions<TenantAccessSettings> options) : ITenantAccessService
{
    private readonly TenantAccessSettings settings = options.Value;

    public Task<bool> HasActiveAccessAsync(
        Guid accountId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return cacheService.GetOrSetAsync(
            Scope(organizationId),
            accountId.ToString(),
            async ct =>
            {
                OrganizationMember? member = await unitOfWork.Repository<OrganizationMember, Guid>()
                    .FirstOrDefaultAsync(
                        m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, ct);

                if (member is null)
                {
                    return false;
                }

                Organization? organization = await unitOfWork.Repository<Organization, Guid>()
                    .GetByIdAsync(organizationId, ct);

                return organization is { Status: true };
            },
            TimeSpan.FromSeconds(settings.CacheTtlSeconds),
            cancellationToken);
    }

    public Task InvalidateMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default) =>
        cacheService.RemoveAsync(Scope(organizationId), accountId.ToString(), cancellationToken);

    public Task InvalidateOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        cacheService.InvalidateScopeAsync(Scope(organizationId), cancellationToken);

    private static string Scope(Guid organizationId) => $"tenant-access:{organizationId}";
}
