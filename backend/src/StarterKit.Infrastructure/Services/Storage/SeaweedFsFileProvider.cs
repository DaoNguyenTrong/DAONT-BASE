using Amazon.S3;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Settings;

namespace StarterKit.Infrastructure.Services.Storage;

internal sealed class SeaweedFsFileProvider(
    IAmazonS3 s3Client,
    IOptions<SeaweedFsSettings> seaweedFsOptions,
    StoragePathGenerator storagePathGenerator)
    : S3FileProvider(s3Client, seaweedFsOptions.Value.BucketName, storagePathGenerator);
