using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.Infrastructure.Services.Storage;

internal sealed class StorageService(IStorageProvider storageProvider) : IStorageService
{
    public Task<StorageUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        return storageProvider.UploadAsync(content, fileName, contentType, cancellationToken);
    }

    public Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken)
    {
        return storageProvider.DownloadAsync(storagePath, cancellationToken);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken)
    {
        return storageProvider.DeleteAsync(storagePath, cancellationToken);
    }

    public Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken)
    {
        return storageProvider.ExistsAsync(storagePath, cancellationToken);
    }
}
