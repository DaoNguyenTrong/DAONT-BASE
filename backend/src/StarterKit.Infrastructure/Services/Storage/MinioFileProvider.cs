using Amazon.S3;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Settings;

namespace StarterKit.Infrastructure.Services.Storage;

internal sealed class MinioFileProvider(
    IAmazonS3 s3Client,
    IOptions<MinioSettings> minioOptions,
    StoragePathGenerator storagePathGenerator)
    : S3FileProvider(s3Client, minioOptions.Value.BucketName, storagePathGenerator);
