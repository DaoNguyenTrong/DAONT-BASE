using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Infrastructure.Services.Storage;

internal sealed class SeaweedFsFileProvider(
    IAmazonS3 s3Client,
    IOptions<SeaweedFsSettings> seaweedFsOptions,
    StoragePathGenerator storagePathGenerator) : IStorageProvider
{
    private readonly SeaweedFsSettings seaweedFsSettings = seaweedFsOptions.Value;

    public async Task<StorageUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        string key = storagePathGenerator.Generate(fileName);

        using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        await s3Client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = seaweedFsSettings.BucketName,
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
            GetObjectResponse response = await s3Client.GetObjectAsync(
                seaweedFsSettings.BucketName, storagePath, cancellationToken);

            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("Stored file content was not found.");
        }
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken)
    {
        await s3Client.DeleteObjectAsync(seaweedFsSettings.BucketName, storagePath, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(seaweedFsSettings.BucketName, storagePath, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
