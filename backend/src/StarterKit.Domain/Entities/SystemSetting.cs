using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public sealed record SystemSettingParams(string Key, string? Value);

public sealed class SystemSetting : BaseEntity
{
    private SystemSetting()
    {
    }

    public static SystemSetting Create(SystemSettingParams p)
    {
        SystemSetting setting = new();
        setting.SetKey(p.Key);
        setting.Value = p.Value;
        return setting;
    }

    public string Key { get; private set; } = string.Empty;
    public string? Value { get; private set; }

    public void UpdateValue(string? value)
    {
        Value = value;
    }

    private void SetKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainException(DomainMessages.SystemSettingKeyRequired);
        }

        Key = key.Trim();
    }
}
