using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record StoredFileParams(
    string FileName,
    string ContentType,
    long Size,
    string StoragePath,
    Guid? OwnerId = null,
    string? Description = null,
    string? Category = null);

public sealed class StoredFile : BaseEntity<Guid>
{
    private StoredFile()
    {
    }

    public static StoredFile Create(StoredFileParams p)
    {
        if (string.IsNullOrWhiteSpace(p.FileName))
        {
            throw new DomainException(DomainMessages.FileNameRequired);
        }

        if (string.IsNullOrWhiteSpace(p.ContentType))
        {
            throw new DomainException(DomainMessages.ContentTypeRequired);
        }

        if (p.Size <= 0)
        {
            throw new DomainException(DomainMessages.FileSizePositive);
        }

        if (string.IsNullOrWhiteSpace(p.StoragePath))
        {
            throw new DomainException(DomainMessages.StoragePathRequired);
        }

        return new StoredFile
        {
            Id = IdGenerator.NewUuidV7(),
            FileName = p.FileName.Trim(),
            ContentType = p.ContentType.Trim(),
            Size = p.Size,
            StoragePath = p.StoragePath.Trim(),
            OwnerId = p.OwnerId,
            Description = string.IsNullOrWhiteSpace(p.Description) ? null : p.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(p.Category) ? null : p.Category.Trim()
        };
    }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public long Size { get; private set; }

    public string StoragePath { get; private set; } = string.Empty;

    public Guid? OwnerId { get; private set; }

    public string? Description { get; private set; }

    public string? Category { get; private set; }
}
