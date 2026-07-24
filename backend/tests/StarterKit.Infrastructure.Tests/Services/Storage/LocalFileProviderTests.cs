using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Exceptions;
using StarterKit.Infrastructure.Services.Storage;

namespace StarterKit.Infrastructure.Tests.Services.Storage;

public class LocalFileProviderTests : IDisposable
{
    private readonly DirectoryInfo tempRoot = Directory.CreateTempSubdirectory("starterkit-storage-tests-");

    private LocalFileProvider CreateProvider()
    {
        IOptions<StorageSettings> options = Options.Create(new StorageSettings
        {
            BasePath = tempRoot.FullName
        });
        IHostEnvironment hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.ContentRootPath.Returns(tempRoot.FullName);

        return new LocalFileProvider(options, hostEnvironment, new StoragePathGenerator());
    }

    public void Dispose()
    {
        if (tempRoot.Exists)
        {
            tempRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task UploadAsync_WritesRealFile_WithCorrectSize()
    {
        LocalFileProvider provider = CreateProvider();
        byte[] bytes = "hello world"u8.ToArray();
        using MemoryStream content = new(bytes);

        StorageUploadResult result = await provider.UploadAsync(content, "note.txt", "text/plain", CancellationToken.None);

        Assert.Equal(bytes.Length, result.Size);
        string fullPath = Path.Combine(tempRoot.FullName, result.StoragePath);
        Assert.True(File.Exists(fullPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(fullPath));
    }

    [Fact]
    public async Task DownloadAsync_MissingFile_ThrowsNotFound()
    {
        LocalFileProvider provider = CreateProvider();

        await Assert.ThrowsAsync<NotFoundException>(
            () => provider.DownloadAsync("2026/01/01/missing.txt", CancellationToken.None));
    }

    [Fact]
    public async Task DownloadAsync_ExistingFile_ReturnsReadableContent()
    {
        LocalFileProvider provider = CreateProvider();
        using MemoryStream content = new("hello"u8.ToArray());
        StorageUploadResult uploaded = await provider.UploadAsync(content, "note.txt", "text/plain", CancellationToken.None);

        await using Stream downloaded = await provider.DownloadAsync(uploaded.StoragePath, CancellationToken.None);
        using StreamReader reader = new(downloaded);
        string text = await reader.ReadToEndAsync();

        Assert.Equal("hello", text);
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_IsNoOp()
    {
        LocalFileProvider provider = CreateProvider();

        await provider.DeleteAsync("2026/01/01/missing.txt", CancellationToken.None);
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesIt()
    {
        LocalFileProvider provider = CreateProvider();
        using MemoryStream content = new("hello"u8.ToArray());
        StorageUploadResult uploaded = await provider.UploadAsync(content, "note.txt", "text/plain", CancellationToken.None);

        await provider.DeleteAsync(uploaded.StoragePath, CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(tempRoot.FullName, uploaded.StoragePath)));
    }

    [Fact]
    public async Task ExistsAsync_ReflectsFilePresence()
    {
        LocalFileProvider provider = CreateProvider();
        using MemoryStream content = new("hello"u8.ToArray());
        StorageUploadResult uploaded = await provider.UploadAsync(content, "note.txt", "text/plain", CancellationToken.None);

        Assert.True(await provider.ExistsAsync(uploaded.StoragePath, CancellationToken.None));
        Assert.False(await provider.ExistsAsync("2026/01/01/missing.txt", CancellationToken.None));
    }

    [Fact]
    public async Task DownloadAsync_PathTraversalOutsideBasePath_ThrowsDomainException()
    {
        LocalFileProvider provider = CreateProvider();

        await Assert.ThrowsAsync<DomainException>(
            () => provider.DownloadAsync("../../etc/passwd", CancellationToken.None));
    }
}
