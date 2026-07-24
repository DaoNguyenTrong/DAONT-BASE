namespace StarterKit.Application.Common.Interfaces;

public interface ISystemSettingsService
{
    Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default);

    Task UpdateSectionAsync(
        string keyPrefix,
        IReadOnlyDictionary<string, string?> values,
        CancellationToken ct = default);
}
