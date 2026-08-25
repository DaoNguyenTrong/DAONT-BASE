using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Exceptions;
using StarterKit.Infrastructure.Services.Storage;

namespace StarterKit.Infrastructure.Tests.Services.Storage;

public class SeaweedFsFileProviderTests
{
    private const string BucketName = "starterkit";

    private sealed record Fixture(SeaweedFsFileProvider Provider, IAmazonS3 S3Client);

    private static Fixture CreateFixture()
    {
        IAmazonS3 s3Client = Substitute.For<IAmazonS3>();
        IOptions<SeaweedFsSettings> options = Options.Create(new SeaweedFsSettings { BucketName = BucketName });
        SeaweedFsFileProvider provider = new(s3Client, options, new StoragePathGenerator());

        return new Fixture(provider, s3Client);
    }

    [Fact]
    public async Task UploadAsync_PutsObjectUnderBucket_AndReturnsKeyWithSize()
    {
        Fixture f = CreateFixture();
        byte[] bytes = "hello world"u8.ToArray();
        using MemoryStream content = new(bytes);

        StorageUploadResult result = await f.Provider.UploadAsync(content, "note.txt", "text/plain", CancellationToken.None);

        Assert.Equal(bytes.Length, result.Size);
        Assert.EndsWith(".txt", result.StoragePath);
        await f.S3Client.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r => r != null && r.BucketName == BucketName && r.Key == result.StoragePath && r.ContentType == "text/plain"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAsync_ExistingKey_ReturnsResponseStream()
    {
        Fixture f = CreateFixture();
        using MemoryStream expectedStream = new("hello"u8.ToArray());
        f.S3Client.GetObjectAsync(BucketName, "path/file.txt", Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = expectedStream });

        Stream result = await f.Provider.DownloadAsync("path/file.txt", CancellationToken.None);

        Assert.Same(expectedStream, result);
    }

    [Fact]
    public async Task DownloadAsync_MissingKey_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        f.S3Client.GetObjectAsync(BucketName, "missing.txt", Arg.Any<CancellationToken>())
            .Returns<GetObjectResponse>(_ => throw new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

        await Assert.ThrowsAsync<NotFoundException>(
            () => f.Provider.DownloadAsync("missing.txt", CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_DelegatesToS3Client()
    {
        Fixture f = CreateFixture();

        await f.Provider.DeleteAsync("path/file.txt", CancellationToken.None);

        await f.S3Client.Received(1).DeleteObjectAsync(BucketName, "path/file.txt", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExistsAsync_ExistingKey_ReturnsTrue()
    {
        Fixture f = CreateFixture();
        f.S3Client.GetObjectMetadataAsync(BucketName, "path/file.txt", Arg.Any<CancellationToken>())
            .Returns(new GetObjectMetadataResponse());

        bool result = await f.Provider.ExistsAsync("path/file.txt", CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_MissingKey_ReturnsFalse()
    {
        Fixture f = CreateFixture();
        f.S3Client.GetObjectMetadataAsync(BucketName, "missing.txt", Arg.Any<CancellationToken>())
            .Returns<GetObjectMetadataResponse>(_ => throw new AmazonS3Exception("Not found") { StatusCode = HttpStatusCode.NotFound });

        bool result = await f.Provider.ExistsAsync("missing.txt", CancellationToken.None);

        Assert.False(result);
    }
}
