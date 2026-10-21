using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Resources;
using StarterKit.Application.Services.Accounts;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Accounts;

public class AccountServiceAvatarTests
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] WebpHeader = "RIFF\0\0\0\0WEBP"u8.ToArray();

    private sealed record Fixture(
        AccountService Service,
        IRepository<Account, Guid> AccountRepo,
        IUnitOfWork UnitOfWork,
        IStorageService Storage,
        Account Account);

    private static Fixture CreateFixture(string? existingAvatar = null)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Account, Guid> accountRepo = Substitute.For<IRepository<Account, Guid>>();
        unitOfWork.Repository<Account, Guid>().Returns(accountRepo);

        Account account = Account.Create(new AccountParams("Nguyen Van A", "nva", "nva@example.com"));
        account.SetAvatar(existingAvatar);
        accountRepo.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(account.Id.ToString());

        IStorageService storage = Substitute.For<IStorageService>();
        storage.UploadAsync(
                Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(new StorageUploadResult("public/avatars/new.png", 12));

        AccountService service = new(
            unitOfWork,
            currentUser,
            Substitute.For<IPasswordHasher>(),
            Substitute.For<IPasswordAttemptStore>(),
            Options.Create(new LoginLockoutSettings()),
            storage,
            Options.Create(new StorageSettings { PublicUrlBase = "http://cdn.test/starterkit/" }),
            NullLogger<AccountService>.Instance);

        return new Fixture(service, accountRepo, unitOfWork, storage, account);
    }

    private static AvatarUploadRequest Request(byte[] content, string contentType = "image/png", long? size = null) =>
        new(new MemoryStream(content), contentType, size ?? content.Length);

    // UploadAvatarAsync

    [Theory]
    [MemberData(nameof(ImageHeaders))]
    public async Task UploadAvatarAsync_ValidImage_StoresUnderPublicAvatars_AndReturnsPublicUrl(
        byte[] header, string expectedContentType)
    {
        Fixture f = CreateFixture();

        ProfileDto profile = await f.Service.UploadAvatarAsync(Request(header), CancellationToken.None);

        Assert.Equal("http://cdn.test/starterkit/public/avatars/new.png", profile.AvatarUrl);
        Assert.Equal("public/avatars/new.png", f.Account.AvatarPath);
        await f.Storage.Received(1).UploadAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), expectedContentType, Arg.Any<CancellationToken>(), "public/avatars");
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    public static TheoryData<byte[], string> ImageHeaders => new()
    {
        { PngHeader, "image/png" },
        { JpegHeader, "image/jpeg" },
        { WebpHeader, "image/webp" },
    };

    [Fact]
    public async Task UploadAvatarAsync_ClientContentTypeLiesButBytesAreNotAnImage_ThrowsInvalidImage()
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.AvatarInvalidImage,
            () => f.Service.UploadAvatarAsync(Request("<svg onload=alert(1)/>"u8.ToArray()), CancellationToken.None));

        await f.Storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task UploadAvatarAsync_EmptyFile_ThrowsFileIsRequired()
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            ApplicationMessages.FileIsRequired,
            () => f.Service.UploadAvatarAsync(Request([], size: 0), CancellationToken.None));
    }

    [Fact]
    public async Task UploadAvatarAsync_TooLarge_ThrowsSizeExceeded_AndDoesNotUpload()
    {
        Fixture f = CreateFixture();

        await Assert.ThrowsAsync<FormattedDomainException>(
            () => f.Service.UploadAvatarAsync(
                Request(PngHeader, size: AccountService.MaxAvatarBytes + 1), CancellationToken.None));

        await f.Storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task UploadAvatarAsync_ExistingAvatar_DeletesTheOldObjectAfterSaving()
    {
        Fixture f = CreateFixture("public/avatars/old.png");

        await f.Service.UploadAvatarAsync(Request(PngHeader), CancellationToken.None);

        await f.Storage.Received(1).DeleteAsync("public/avatars/old.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadAvatarAsync_DeletingTheOldObjectFails_StillSucceeds()
    {
        Fixture f = CreateFixture("public/avatars/old.png");
        f.Storage.DeleteAsync("public/avatars/old.png", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("storage down")));

        ProfileDto profile = await f.Service.UploadAvatarAsync(Request(PngHeader), CancellationToken.None);

        Assert.NotNull(profile.AvatarUrl);
    }

    [Fact]
    public async Task UploadAvatarAsync_SaveFails_RemovesTheNewObject()
    {
        Fixture f = CreateFixture();
        f.UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Service.UploadAvatarAsync(Request(PngHeader), CancellationToken.None));

        await f.Storage.Received(1).DeleteAsync("public/avatars/new.png", Arg.Any<CancellationToken>());
    }

    // DeleteAvatarAsync

    [Fact]
    public async Task DeleteAvatarAsync_WithAvatar_ClearsPath_AndDeletesObject()
    {
        Fixture f = CreateFixture("public/avatars/old.png");

        ProfileDto profile = await f.Service.DeleteAvatarAsync(CancellationToken.None);

        Assert.Null(profile.AvatarUrl);
        Assert.Null(f.Account.AvatarPath);
        await f.Storage.Received(1).DeleteAsync("public/avatars/old.png", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAvatarAsync_WithoutAvatar_IsNoOp()
    {
        Fixture f = CreateFixture();

        ProfileDto profile = await f.Service.DeleteAvatarAsync(CancellationToken.None);

        Assert.Null(profile.AvatarUrl);
        await f.Storage.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }

    // GetCurrentProfileAsync

    [Fact]
    public async Task GetCurrentProfileAsync_WithAvatar_ReturnsPublicUrl()
    {
        Fixture f = CreateFixture("public/avatars/old.png");

        ProfileDto profile = await f.Service.GetCurrentProfileAsync(CancellationToken.None);

        Assert.Equal("http://cdn.test/starterkit/public/avatars/old.png", profile.AvatarUrl);
    }
}
