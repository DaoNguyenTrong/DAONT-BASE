using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Settings;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Infrastructure.Services.Storage;

internal sealed class LocalFileProvider(
    IOptions<StorageSettings> storageOptions,
    IHostEnvironment hostEnvironment,
    StoragePathGenerator storagePathGenerator) : IStorageProvider
{
    private readonly StorageSettings storageSettings = storageOptions.Value;

    public async Task<StorageUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        string storagePath = storagePathGenerator.Generate(fileName);
        string fullPath = GetFullPath(storagePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using FileStream fileStream = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(fileStream, cancellationToken);

        return new StorageUploadResult(storagePath, fileStream.Length);
    }

    public Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken)
    {
        string fullPath = GetFullPath(storagePath);

        if (!File.Exists(fullPath))
        {
            throw new NotFoundException("Stored file content was not found.");
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken)
    {
        string fullPath = GetFullPath(storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken)
    {
        return Task.FromResult(File.Exists(GetFullPath(storagePath)));
    }

    private string GetFullPath(string storagePath)
    {
        string basePath = Path.IsPathRooted(storageSettings.BasePath)
            ? storageSettings.BasePath
            : Path.Combine(hostEnvironment.ContentRootPath, storageSettings.BasePath);

        string fullBasePath = Path.GetFullPath(basePath);
        string fullPath = Path.GetFullPath(Path.Combine(fullBasePath, storagePath));
        string relativePath = Path.GetRelativePath(fullBasePath, fullPath);

        if (relativePath == "." ||
            relativePath.StartsWith("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new DomainException("Invalid storage path.");
        }

        return fullPath;
    }
}
