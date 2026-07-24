using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Storage;

namespace StarterKit.Infrastructure.Tests.Services.Storage;

public class StorageServiceTests
{
    private sealed record Fixture(StorageService Service, IStorageProvider Provider);

    private static Fixture CreateFixture()
    {
        IStorageProvider provider = Substitute.For<IStorageProvider>();
        StorageService service = new(provider);

        return new Fixture(service, provider);
    }

    [Fact]
    public async Task UploadAsync_DelegatesToProvider()
    {
        Fixture f = CreateFixture();
        using MemoryStream content = new();
        StorageUploadResult expected = new("path/file.png", 100);
        f.Provider.UploadAsync(content, "file.png", "image/png", Arg.Any<CancellationToken>()).Returns(expected);

        StorageUploadResult result = await f.Service.UploadAsync(content, "file.png", "image/png", CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task DownloadAsync_DelegatesToProvider()
    {
        Fixture f = CreateFixture();
        using MemoryStream expected = new();
        f.Provider.DownloadAsync("path/file.png", Arg.Any<CancellationToken>()).Returns(expected);

        Stream result = await f.Service.DownloadAsync("path/file.png", CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToProvider()
    {
        Fixture f = CreateFixture();

        await f.Service.DeleteAsync("path/file.png", CancellationToken.None);

        await f.Provider.Received(1).DeleteAsync("path/file.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistsAsync_DelegatesToProvider()
    {
        Fixture f = CreateFixture();
        f.Provider.ExistsAsync("path/file.png", Arg.Any<CancellationToken>()).Returns(true);

        bool result = await f.Service.ExistsAsync("path/file.png", CancellationToken.None);

        Assert.True(result);
    }
}
