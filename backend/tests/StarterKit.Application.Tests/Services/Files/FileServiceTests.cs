using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Files;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Files;

public class FileServiceTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    private sealed record Fixture(
        FileService Service,
        IStorageService StorageService,
        IRepository<StoredFile, Guid> FileRepo,
        IUnitOfWork UnitOfWork);

    private static Fixture CreateFixture(
        long maxFileSizeBytes = 10_485_760,
        string[]? allowedContentTypes = null,
        string publicUrlBase = "/storage",
        bool hasActiveOrganization = true)
    {
        IStorageService storageService = Substitute.For<IStorageService>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<StoredFile, Guid> fileRepo = Substitute.For<IRepository<StoredFile, Guid>>();
        unitOfWork.Repository<StoredFile, Guid>().Returns(fileRepo);
        ICurrentTenantProvider currentTenantProvider = Substitute.For<ICurrentTenantProvider>();
        currentTenantProvider.OrganizationId.Returns(hasActiveOrganization ? OrganizationId : (Guid?)null);

        IOptions<StorageSettings> options = Options.Create(new StorageSettings
        {
            MaxFileSizeBytes = maxFileSizeBytes,
            AllowedContentTypes = allowedContentTypes ?? [],
            PublicUrlBase = publicUrlBase
        });

        FileService service = new(storageService, unitOfWork, currentTenantProvider, options);

        return new Fixture(service, storageService, fileRepo, unitOfWork);
    }

    private static UploadFileRequest CreateUploadRequest(
        long size = 100, string contentType = "image/png", string fileName = "avatar.png") =>
        new(new MemoryStream(), fileName, contentType, size);

    private static StoredFile CreateStoredFile(
        string fileName = "avatar.png",
        string contentType = "image/png",
        long size = 100,
        string storagePath = "2026/07/24/abc123.png")
    {
        return StoredFile.Create(new StoredFileParams(fileName, contentType, size, storagePath, OrganizationId));
    }

    // UploadAsync

    [Fact]
    public async Task UploadAsync_NoActiveOrganization_ThrowsForbidden_AndDoesNotCallStorage()
    {
        Fixture f = CreateFixture(hasActiveOrganization: false);
        UploadFileRequest request = CreateUploadRequest();

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.UploadAsync(request, CancellationToken.None));

        await f.StorageService.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_SizeZeroOrLess_ThrowsDomain_AndDoesNotCallStorage()
    {
        Fixture f = CreateFixture();
        UploadFileRequest request = CreateUploadRequest(size: 0);

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.FileIsRequired,
            () => f.Service.UploadAsync(request, CancellationToken.None));

        await f.StorageService.DidNotReceive().UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAsync_SizeExceedsMax_ThrowsFormattedDomainExceptionWithMaxSize()
    {
        Fixture f = CreateFixture(maxFileSizeBytes: 1000);
        UploadFileRequest request = CreateUploadRequest(size: 2000);

        FormattedDomainException ex = await Assert.ThrowsAsync<FormattedDomainException>(
            () => f.Service.UploadAsync(request, CancellationToken.None));

        Assert.Equal(ApplicationMessages.FileSizeExceeded, ex.Message);
        Assert.Equal(1000L, ex.Args[0]);
    }

    [Fact]
    public async Task UploadAsync_ContentTypeNotAllowed_ThrowsDomain()
    {
        Fixture f = CreateFixture(allowedContentTypes: ["image/jpeg"]);
        UploadFileRequest request = CreateUploadRequest(contentType: "image/png");

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.FileContentTypeNotAllowed,
            () => f.Service.UploadAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task UploadAsync_ContentTypeAllowed_CaseInsensitive_Succeeds()
    {
        Fixture f = CreateFixture(allowedContentTypes: ["IMAGE/PNG"]);
        UploadFileRequest request = CreateUploadRequest(contentType: "image/png");
        f.StorageService.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StorageUploadResult("path/file.png", 100));

        FileDto dto = await f.Service.UploadAsync(request, CancellationToken.None);

        Assert.Equal("image/png", dto.ContentType);
    }

    [Fact]
    public async Task UploadAsync_EmptyAllowedContentTypes_AllowsAnyType()
    {
        Fixture f = CreateFixture(allowedContentTypes: []);
        UploadFileRequest request = CreateUploadRequest(contentType: "application/octet-stream");
        f.StorageService.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StorageUploadResult("path/file.bin", 100));

        FileDto dto = await f.Service.UploadAsync(request, CancellationToken.None);

        Assert.Equal("application/octet-stream", dto.ContentType);
    }

    [Fact]
    public async Task UploadAsync_PersistsSizeFromStorageResult_NotFromRequest()
    {
        Fixture f = CreateFixture();
        UploadFileRequest request = CreateUploadRequest(size: 100);
        f.StorageService.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StorageUploadResult("path/file.png", 999));

        FileDto dto = await f.Service.UploadAsync(request, CancellationToken.None);

        Assert.Equal(999, dto.Size);
        await f.FileRepo.Received(1).AddAsync(
            Arg.Is<StoredFile>(sf => sf != null && sf.Size == 999), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/storage", "path/file.png", "/storage/path/file.png")]
    [InlineData("/storage/", "/path/file.png", "/storage/path/file.png")]
    [InlineData("/storage", "path/file.png/", "/storage/path/file.png/")]
    public async Task UploadAsync_BuildsPublicUrl_WithoutDoubleOrMissingSlash(
        string publicUrlBase, string storagePath, string expectedUrl)
    {
        Fixture f = CreateFixture(publicUrlBase: publicUrlBase);
        UploadFileRequest request = CreateUploadRequest();
        f.StorageService.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new StorageUploadResult(storagePath, 100));

        FileDto dto = await f.Service.UploadAsync(request, CancellationToken.None);

        Assert.Equal(expectedUrl, dto.PublicUrl);
    }

    // GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_NoActiveOrganization_ThrowsForbidden()
    {
        Fixture f = CreateFixture(hasActiveOrganization: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        f.FileRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((StoredFile?)null);

        await ApplicationAssert.AssertNotFoundAsync<StoredFile>(id, () => f.Service.GetByIdAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsDto()
    {
        Fixture f = CreateFixture();
        StoredFile storedFile = CreateStoredFile();
        f.FileRepo.GetByIdAsync(storedFile.Id, Arg.Any<CancellationToken>()).Returns(storedFile);

        FileDto dto = await f.Service.GetByIdAsync(storedFile.Id, CancellationToken.None);

        Assert.Equal(storedFile.Id, dto.Id);
    }

    // GetAllAsync

    [Fact]
    public async Task GetAllAsync_NoActiveOrganization_ThrowsForbidden()
    {
        Fixture f = CreateFixture(hasActiveOrganization: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.GetAllAsync(new FileListRequest(), CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 0, 1, 10)]
    [InlineData(-1, -1, 1, 10)]
    public async Task GetAllAsync_DefaultsInvalidPageValues(
        int requestPage, int requestSize, int expectedPage, int expectedSize)
    {
        Fixture f = CreateFixture();
        f.FileRepo.ListPagedAsync(
                Arg.Any<Expression<Func<StoredFile, bool>>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<StoredFile>)[], 0));

        await f.Service.GetAllAsync(new FileListRequest(requestPage, requestSize), CancellationToken.None);

        await f.FileRepo.Received(1).ListPagedAsync(
            Arg.Any<Expression<Func<StoredFile, bool>>>(), expectedPage, expectedSize, Arg.Any<CancellationToken>());
    }

    // DownloadAsync

    [Fact]
    public async Task DownloadAsync_NotFound_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        f.FileRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((StoredFile?)null);

        await ApplicationAssert.AssertNotFoundAsync<StoredFile>(id, () => f.Service.DownloadAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task DownloadAsync_Found_ReturnsContentFromStorage()
    {
        Fixture f = CreateFixture();
        StoredFile storedFile = CreateStoredFile(fileName: "report.pdf", contentType: "application/pdf");
        f.FileRepo.GetByIdAsync(storedFile.Id, Arg.Any<CancellationToken>()).Returns(storedFile);
        using MemoryStream content = new();
        f.StorageService.DownloadAsync(storedFile.StoragePath, Arg.Any<CancellationToken>()).Returns(content);

        FileDownloadResult result = await f.Service.DownloadAsync(storedFile.Id, CancellationToken.None);

        Assert.Same(content, result.Content);
        Assert.Equal("report.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
    }

    // DeleteAsync

    [Fact]
    public async Task DeleteAsync_NotFound_ThrowsNotFound_AndDoesNotCallStorage()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        f.FileRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((StoredFile?)null);

        await ApplicationAssert.AssertNotFoundAsync<StoredFile>(id, () => f.Service.DeleteAsync(id, CancellationToken.None));

        await f.StorageService.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Found_DeletesFromStorageBeforeDb()
    {
        Fixture f = CreateFixture();
        StoredFile storedFile = CreateStoredFile();
        f.FileRepo.GetByIdAsync(storedFile.Id, Arg.Any<CancellationToken>()).Returns(storedFile);

        await f.Service.DeleteAsync(storedFile.Id, CancellationToken.None);

        Received.InOrder(() =>
        {
            f.StorageService.DeleteAsync(storedFile.StoragePath, Arg.Any<CancellationToken>());
            f.FileRepo.Delete(storedFile);
        });
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
