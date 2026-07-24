using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files");

        builder.HasKey(file => file.Id);

        builder.Property(file => file.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(file => file.ContentType)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(file => file.Size)
            .IsRequired();

        builder.Property(file => file.StoragePath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(file => file.Description)
            .HasMaxLength(1000);

        builder.Property(file => file.Category)
            .HasMaxLength(100);

        builder.Property(file => file.CreatedAt)
            .IsRequired();

        builder.HasIndex(file => file.StoragePath)
            .IsUnique();

        builder.HasIndex(file => file.OwnerId);

        builder.HasIndex(file => file.Category);
    }
}
