using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Resources;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.SystemSettings;

public sealed class SystemSettingsService(
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    ICurrentTenantProvider currentTenantProvider) : ISystemSettingsService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default)
    {
        Guid organizationId = RequireOrganizationId();

        return cacheService.GetOrSetAsync(Scope(organizationId), CacheKey, async token =>
        {
            IRepository<SystemSetting> repository = unitOfWork.Repository<SystemSetting>();
            IReadOnlyList<SystemSetting> rows = await repository.ListAsync(
                s => s.OrganizationId == organizationId, token);

            return (IReadOnlyDictionary<string, string?>)rows.ToDictionary(row => row.Key, row => row.Value);
        }, CacheDuration, ct);
    }

    public async Task UpdateSectionAsync(
        string keyPrefix,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken ct = default)
    {
        Guid organizationId = RequireOrganizationId();
        IRepository<SystemSetting> repository = unitOfWork.Repository<SystemSetting>();

        foreach ((string propertyName, string? value) in values)
        {
            string fullKey = keyPrefix + propertyName;
            SystemSetting? existing = await repository.FirstOrDefaultAsync(
                s => s.OrganizationId == organizationId && s.Key == fullKey, ct);

            if (existing is null)
            {
                await repository.AddAsync(
                    SystemSetting.Create(new SystemSettingParams(fullKey, value), organizationId), ct);
            }
            else
            {
                existing.UpdateValue(value);
                repository.Update(existing);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        await cacheService.RemoveAsync(Scope(organizationId), CacheKey, ct);
    }

    private Guid RequireOrganizationId() =>
        currentTenantProvider.OrganizationId
            ?? throw new ForbiddenException(ApplicationMessages.OrganizationAccessDenied);

    private const string CacheKey = "all";

    private static string Scope(Guid organizationId) => $"systemsettings:{organizationId}";
}
