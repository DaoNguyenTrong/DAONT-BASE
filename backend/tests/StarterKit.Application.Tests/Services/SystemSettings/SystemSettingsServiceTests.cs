using System.Linq.Expressions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.SystemSettings;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.SystemSettings;

public class SystemSettingsServiceTests
{
    private sealed record Fixture(SystemSettingsService Service, IRepository<SystemSetting> Repo, IUnitOfWork UnitOfWork, ICacheService CacheService);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<SystemSetting> repo = Substitute.For<IRepository<SystemSetting>>();
        unitOfWork.Repository<SystemSetting>().Returns(repo);

        ICacheService cacheService = Substitute.For<ICacheService>();
        cacheService.GetOrSetAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>>>()!(CancellationToken.None));

        StarterKit.Application.Services.SystemSettings.SystemSettingsService service = new(unitOfWork, cacheService);

        return new Fixture(service, repo, unitOfWork, cacheService);
    }

    private static SystemSetting CreateSetting(string key, string? value) =>
        SystemSetting.Create(new SystemSettingParams(key, value));

    // GetAllAsync

    [Fact]
    public async Task GetAllAsync_InvokesFactory_AndMapsRowsToDictionary()
    {
        Fixture f = CreateFixture();
        SystemSetting row1 = CreateSetting("app:name", "StarterKit");
        SystemSetting row2 = CreateSetting("app:nullable", null);
        f.Repo.ListAsync(Arg.Any<Expression<Func<SystemSetting, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SystemSetting>)[row1, row2]);

        IReadOnlyDictionary<string, string?> result = await f.Service.GetAllAsync(CancellationToken.None);

        Assert.Equal("StarterKit", result["app:name"]);
        Assert.Null(result["app:nullable"]);
        await f.Repo.Received(1).ListAsync(Arg.Any<Expression<Func<SystemSetting, bool>>>(), Arg.Any<CancellationToken>());
    }

    // UpdateSectionAsync

    [Fact]
    public async Task UpdateSectionAsync_MixedCreateAndUpdate_SavesOnceAndInvalidatesCacheOnceAfterSave()
    {
        Fixture f = CreateFixture();
        SystemSetting existing = CreateSetting("app:name", "Old");
        RepositoryPredicateStub.StubFirstOrDefault(f.Repo, [existing]);
        Dictionary<string, string?> values = new()
        {
            ["name"] = "New",
            ["newKey"] = "New Value"
        };

        await f.Service.UpdateSectionAsync("app:", values, CancellationToken.None);

        Assert.Equal("New", existing.Value);
        f.Repo.Received(1).Update(existing);
        await f.Repo.Received(1).AddAsync(
            Arg.Is<SystemSetting>(s => s != null && s.Key == "app:newKey" && s.Value == "New Value"),
            Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.CacheService.Received(1).RemoveAsync("systemsettings:all", Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            f.CacheService.RemoveAsync("systemsettings:all", Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task UpdateSectionAsync_EmptyDictionary_StillSavesAndInvalidatesOnce()
    {
        Fixture f = CreateFixture();

        await f.Service.UpdateSectionAsync("app:", new Dictionary<string, string?>(), CancellationToken.None);

        await f.Repo.DidNotReceive().AddAsync(Arg.Any<SystemSetting>(), Arg.Any<CancellationToken>());
        f.Repo.DidNotReceive().Update(Arg.Any<SystemSetting>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.CacheService.Received(1).RemoveAsync("systemsettings:all", Arg.Any<CancellationToken>());
    }
}
