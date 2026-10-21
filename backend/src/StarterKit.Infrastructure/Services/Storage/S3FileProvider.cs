using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Infrastructure.Services.Storage;

internal abstract class S3FileProvider(
    IAmazonS3 s3Client,
    string bucketName,
    StoragePathGenerator storagePathGenerator) : IStorageProvider
{
    public async Task<StorageUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken,
        string? folder = null)
    {
        string key = storagePathGenerator.Generate(fileName, folder);

        using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        await s3Client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = buffer,
                ContentType = contentType,
                AutoCloseStream = false
            },
            cancellationToken);

        return new StorageUploadResult(key, buffer.Length);
    }

    public async Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken)
    {
        try
        {
            GetObjectResponse response = await s3Client.GetObjectAsync(bucketName, storagePath, cancellationToken);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Stored file content was not found.");
        }
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken)
    {
        await s3Client.DeleteObjectAsync(bucketName, storagePath, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(bucketName, storagePath, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
