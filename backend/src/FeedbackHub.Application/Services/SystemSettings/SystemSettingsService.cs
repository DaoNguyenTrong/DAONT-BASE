using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.SystemSettings;

public sealed class SystemSettingsService(
    IUnitOfWork unitOfWork,
    ICacheService cacheService) : ISystemSettingsService
{
    private const string CacheKey = "systemsettings:all";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default) =>
        cacheService.GetOrSetAsync(CacheKey, async token =>
        {
            IRepository<SystemSetting> repository = unitOfWork.Repository<SystemSetting>();
            IReadOnlyList<SystemSetting> rows = await repository.ListAsync(_ => true, token);

            return (IReadOnlyDictionary<string, string?>)rows.ToDictionary(row => row.Key, row => row.Value);
        }, CacheDuration, ct);

    public async Task UpdateSectionAsync(
        string keyPrefix,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken ct = default)
    {
        IRepository<SystemSetting> repository = unitOfWork.Repository<SystemSetting>();

        foreach ((string propertyName, string? value) in values)
        {
            string fullKey = keyPrefix + propertyName;
            SystemSetting? existing = await repository.FirstOrDefaultAsync(s => s.Key == fullKey, ct);

            if (existing is null)
            {
                await repository.AddAsync(SystemSetting.Create(new SystemSettingParams(fullKey, value)), ct);
            }
            else
            {
                existing.UpdateValue(value);
                repository.Update(existing);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        await cacheService.RemoveAsync(CacheKey, ct);
    }
}
