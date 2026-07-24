using Microsoft.EntityFrameworkCore;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Seeding;

public static class SystemSettingSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken ct = default)
    {
        List<string> existingKeys = await dbContext.SystemSettings
            .Select(setting => setting.Key)
            .ToListAsync(ct);

        List<string> missingKeys = SystemSettingDefaults.All.Keys.Except(existingKeys).ToList();

        if (missingKeys.Count == 0)
        {
            return;
        }

        foreach (string key in missingKeys)
        {
            dbContext.SystemSettings.Add(SystemSetting.Create(new SystemSettingParams(key, SystemSettingDefaults.All[key])));
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
