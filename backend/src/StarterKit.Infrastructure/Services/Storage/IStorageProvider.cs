using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Storage;

internal interface IStorageProvider
{
    Task<StorageUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);

    Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken);

    Task DeleteAsync(string storagePath, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken);
}
