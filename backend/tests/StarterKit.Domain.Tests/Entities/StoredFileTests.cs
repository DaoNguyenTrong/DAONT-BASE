using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class StoredFileTests
{
    private static StoredFileParams ValidParams()
    {
        return new StoredFileParams(
            FileName: "report.pdf",
            ContentType: "application/pdf",
            Size: 1024,
            StoragePath: "/files/report.pdf",
            OwnerId: null,
            Description: null,
            Category: null);
    }

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        StoredFileParams p = ValidParams();

        StoredFile file = StoredFile.Create(p);

        Assert.NotEqual(Guid.Empty, file.Id);
        Assert.Equal(p.FileName, file.FileName);
        Assert.Equal(p.ContentType, file.ContentType);
        Assert.Equal(p.Size, file.Size);
        Assert.Equal(p.StoragePath, file.StoragePath);
        Assert.Null(file.OwnerId);
        Assert.Null(file.Description);
        Assert.Null(file.Category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankFileName_ThrowsDomainException(string fileName)
    {
        StoredFileParams p = ValidParams() with { FileName = fileName };

        DomainAssert.ThrowsWithMessage(DomainMessages.FileNameRequired, () => StoredFile.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankContentType_ThrowsDomainException(string contentType)
    {
        StoredFileParams p = ValidParams() with { ContentType = contentType };

        DomainAssert.ThrowsWithMessage(DomainMessages.ContentTypeRequired, () => StoredFile.Create(p));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveSize_ThrowsDomainException(long size)
    {
        StoredFileParams p = ValidParams() with { Size = size };

        DomainAssert.ThrowsWithMessage(DomainMessages.FileSizePositive, () => StoredFile.Create(p));
    }

    [Fact]
    public void Create_WithSizeOfOne_DoesNotThrow()
    {
        StoredFileParams p = ValidParams() with { Size = 1 };

        StoredFile file = StoredFile.Create(p);

        Assert.Equal(1, file.Size);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankStoragePath_ThrowsDomainException(string storagePath)
    {
        StoredFileParams p = ValidParams() with { StoragePath = storagePath };

        DomainAssert.ThrowsWithMessage(DomainMessages.StoragePathRequired, () => StoredFile.Create(p));
    }

    [Fact]
    public void Create_TrimsRequiredAndOptionalStringFields()
    {
        StoredFileParams p = ValidParams() with
        {
            FileName = " report.pdf ",
            ContentType = " application/pdf ",
            StoragePath = " /files/report.pdf ",
            Description = " desc ",
            Category = " cat "
        };

        StoredFile file = StoredFile.Create(p);

        Assert.Equal("report.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("/files/report.pdf", file.StoragePath);
        Assert.Equal("desc", file.Description);
        Assert.Equal("cat", file.Category);
    }

    [Fact]
    public void Create_WithOwnerId_AssignsWithoutValidation()
    {
        Guid ownerId = Guid.NewGuid();
        StoredFileParams p = ValidParams() with { OwnerId = ownerId };

        StoredFile file = StoredFile.Create(p);

        Assert.Equal(ownerId, file.OwnerId);
    }
}
