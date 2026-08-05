using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class SystemSettingTests
{
    [Fact]
    public void Create_WithValidParams_AssignsKeyAndValue()
    {
        SystemSetting setting = SystemSetting.Create(new SystemSettingParams("retrieval.topK", "10"), Guid.NewGuid());

        Assert.Equal("retrieval.topK", setting.Key);
        Assert.Equal("10", setting.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankKey_ThrowsDomainException(string key)
    {
        DomainAssert.ThrowsWithMessage(DomainMessages.SystemSettingKeyRequired, () => SystemSetting.Create(new SystemSettingParams(key, "10"), Guid.NewGuid()));
    }

    [Fact]
    public void Create_TrimsKey()
    {
        SystemSetting setting = SystemSetting.Create(new SystemSettingParams("  retrieval.topK  ", "10"), Guid.NewGuid());

        Assert.Equal("retrieval.topK", setting.Key);
    }

    [Fact]
    public void Create_WithNullValue_AssignsNullWithoutThrowing()
    {
        SystemSetting setting = SystemSetting.Create(new SystemSettingParams("retrieval.topK", null), Guid.NewGuid());

        Assert.Null(setting.Value);
    }

    [Fact]
    public void Create_DoesNotTrimValue()
    {
        SystemSetting setting = SystemSetting.Create(new SystemSettingParams("retrieval.topK", "  10  "), Guid.NewGuid());

        Assert.Equal("  10  ", setting.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("new-value")]
    public void UpdateValue_AssignsRawValueUnconditionally(string? value)
    {
        SystemSetting setting = SystemSetting.Create(new SystemSettingParams("retrieval.topK", "10"), Guid.NewGuid());

        setting.UpdateValue(value);

        Assert.Equal(value, setting.Value);
    }
}
