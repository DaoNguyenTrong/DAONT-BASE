namespace StarterKit.Application.Common.Interfaces;

public interface IStorageService
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

public sealed record StorageUploadResult(string StoragePath, long Size);
