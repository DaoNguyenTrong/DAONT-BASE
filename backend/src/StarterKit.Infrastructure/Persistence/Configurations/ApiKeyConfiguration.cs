using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");

        builder.HasKey(key => key.Id);

        builder.Property(key => key.Id)
            .HasColumnName("id");

        builder.Property(key => key.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(key => key.KeyPrefix)
            .HasColumnName("key_prefix")
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(key => key.KeyHash)
            .HasColumnName("key_hash")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(key => key.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(key => key.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(key => key.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(key => key.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(key => key.UpdatedBy)
            .HasColumnName("updated_by");

        builder.Property(key => key.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.HasIndex(key => key.KeyHash)
            .IsUnique();

        builder.HasIndex(key => key.KeyPrefix);

        builder.HasIndex(key => key.OrganizationId);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(key => key.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
